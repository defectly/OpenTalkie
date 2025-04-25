using Android.Media;
using Android.Media.Projection;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;
using Encoding = Android.Media.Encoding;

namespace OpenTalkie.Platforms.Android.Common.Services.Playback;

public class PlaybackService : IPlaybackService
{
    private Encoding _encoding;
    private static float _volume;
    private int _sampleRate;
    private ChannelOut _channelOut;

    private AudioRecord? _audioRecord;
    private WaveFormat? _waveFormat;
    private IPlaybackRepository playbackRepository;
    private IScreenAudioCapturing audioRecording;

    public PlaybackService(IPlaybackRepository playbackRepository, IScreenAudioCapturing audioRecording)
    {
        this.playbackRepository = playbackRepository;
        this.audioRecording = audioRecording;

        playbackRepository.VolumeChanged += OnVolumeChange;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is not created");

        int read = await _audioRecord.ReadAsync(buffer, offset, count);

        ChangeVolume(buffer, _volume);

        return read;
    }

    public static unsafe void ChangeVolume(byte[] audioBytes, float gain)
    {
        if (audioBytes == null || audioBytes.Length % 2 != 0)
            throw new ArgumentException("Incorrect audio data format");

        fixed (byte* ptr = audioBytes)
        {
            short* samples = (short*)ptr;
            int sampleCount = audioBytes.Length / 2;

            for (int i = 0; i < sampleCount; i++)
            {
                float amplified = samples[i] * gain;
                samples[i] = amplified > short.MaxValue ? short.MaxValue :
                             amplified < short.MinValue ? short.MinValue :
                             (short)amplified;
            }
        }
    }

    private void LoadPreferences()
    {
        _encoding = (Encoding)Preferences.Get("PlaybackEncoding", (int)Encoding.Default);
        _sampleRate = Preferences.Get("PlaybackSampleRate", 48000);
        _channelOut = (ChannelOut)Preferences.Get("PlaybackChannelOut", (int)ChannelOut.Stereo);
        _volume = Preferences.Get("PlaybackVolume", 1f);
    }

    public void Start()
    {
        if (_audioRecord != null)
            return;

        var mediaProjection = audioRecording.GetMediaProjection();

        if (mediaProjection == null)
            throw new NullReferenceException($"Media projection not provided");

        LoadPreferences();
        CreateAudioRecord(mediaProjection);

        if (_audioRecord == null || _audioRecord.State == State.Uninitialized)
        {
            _audioRecord = null;
            audioRecording.StopRecording();
            throw new Exception("Can't initialize audio record.. Selected parameters may be not supported");
        }

        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record not created");

        _audioRecord.StartRecording();

        return;
    }

    public async Task<bool> RequestPermissionAsync()
    {
        return await audioRecording.StartRecording();
    }

    private void CreateAudioRecord(MediaProjection mediaProjection)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            throw new NotSupportedException($"Minimum android version is 10 (SDK 29)");

        var config = new AudioPlaybackCaptureConfiguration.Builder(mediaProjection)
        .AddMatchingUsage(AudioUsageKind.Media)
        .Build();

        var audioFormat = new AudioFormat.Builder()
            .SetEncoding(_encoding)
            ?.SetSampleRate(_sampleRate)
            ?.SetChannelMask(_channelOut)
            .Build();

        if (audioFormat == null)
            throw new Exception($"Couldn't create audio format");

        _audioRecord = new AudioRecord.Builder()
            ?.SetAudioPlaybackCaptureConfig(config)
            ?.SetAudioFormat(audioFormat)
            ?.Build();
    }

    public void Stop()
    {
        if (_audioRecord == null)
            return;

        if (_audioRecord.RecordingState == RecordState.Stopped)
            return;

        _audioRecord.Stop();
        _audioRecord.Dispose();
        _audioRecord = null;
        audioRecording.StopRecording();
        _waveFormat = null;
    }

    public int GetBufferSize()
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is not created");

        return _audioRecord.BufferSizeInFrames;
    }

    public WaveFormat GetWaveFormat()
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is null");

        return _waveFormat ??= new WaveFormat(int.Parse(playbackRepository.GetSelectedEncoding()), _audioRecord.ChannelCount, _audioRecord.SampleRate);
    }

    private static void OnVolumeChange(float gain)
    {
        _volume = gain;
    }
}
