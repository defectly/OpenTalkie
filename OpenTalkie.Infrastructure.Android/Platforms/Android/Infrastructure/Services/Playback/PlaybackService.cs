using Android.Media;
using Android.Media.Projection;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Models;
using Encoding = Android.Media.Encoding;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services.Playback;

public class PlaybackService : IPlaybackService
{
    private Encoding _encoding;
    private static float _volume;
    private int _sampleRate;
    private ChannelOut _channelOut;

    private AudioRecord? _audioRecord;
    private WaveFormat? _waveFormat;
    private readonly IPlaybackRepository playbackRepository;
    private readonly MediaProjectionProvider mediaProjectionProvider;

    public PlaybackService(IPlaybackRepository playbackRepository, MediaProjectionProvider mediaProjectionProvider)
    {
        this.playbackRepository = playbackRepository;
        this.mediaProjectionProvider = mediaProjectionProvider;

        playbackRepository.VolumeChanged += OnVolumeChange;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord == null)
            throw new NullReferenceException("Audio record is not created");

        int read = await _audioRecord.ReadAsync(buffer, offset, count);

        if (read > 0)
            ChangeVolume(buffer, offset, read, _volume);

        return read;
    }

    public static unsafe void ChangeVolume(byte[] audioBytes, int offset, int length, float gain)
    {
        if (audioBytes == null || length % 2 != 0)
            throw new ArgumentException("Incorrect audio data format");

        fixed (byte* basePtr = audioBytes)
        {
            byte* ptr = basePtr + offset;
            short* samples = (short*)ptr;
            int sampleCount = length / 2;

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
        var settings = playbackRepository.GetSettings();
        _encoding = MapToAndroidEncoding(int.Parse(settings.SelectedEncoding.Value));
        _sampleRate = int.Parse(settings.SelectedSampleRate.Value);
        _channelOut = Enum.Parse<ChannelOut>(settings.SelectedChannelOut.Value);
        _volume = settings.VolumeGain;
    }

    public void Start()
    {
        if (_audioRecord != null)
            return;

        try
        {
            var mediaProjection = mediaProjectionProvider.GetActiveProjection();

            if (mediaProjection == null)
                throw new NullReferenceException("Media projection not provided");

            LoadPreferences();
            CreateAudioRecord(mediaProjection);

            if (_audioRecord == null || _audioRecord.State == State.Uninitialized)
            {
                _audioRecord = null;
                mediaProjectionProvider.StopCapture();
                throw new Exception("Can't initialize audio record.. Selected parameters may be not supported");
            }

            _audioRecord.StartRecording();
        }
        catch
        {
            try { mediaProjectionProvider.StopCapture(); } catch { }
            throw;
        }
    }

    public async Task<bool> RequestPermissionAsync()
    {
        return await mediaProjectionProvider.RequestCaptureAsync();
    }

    private void CreateAudioRecord(MediaProjection mediaProjection)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            throw new NotSupportedException("Minimum android version is 10 (SDK 29)");

        var config = new AudioPlaybackCaptureConfiguration.Builder(mediaProjection)
            .AddMatchingUsage(AudioUsageKind.Media)
            .AddMatchingUsage(AudioUsageKind.Game)
            .AddMatchingUsage(AudioUsageKind.Unknown)
            .Build();

        var audioFormat = new AudioFormat.Builder()
            .SetEncoding(_encoding)
            ?.SetSampleRate(_sampleRate)
            ?.SetChannelMask(_channelOut)
            .Build();

        if (audioFormat == null)
            throw new Exception("Couldn't create audio format");

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
        mediaProjectionProvider.StopCapture();
        _waveFormat = null;
    }

    public int GetBufferSize()
    {
        if (_audioRecord == null)
            throw new NullReferenceException("Audio record is not created");

        int frames = _audioRecord.BufferSizeInFrames;
        int channels = _audioRecord.ChannelCount;
        int bps = _encoding switch
        {
            Encoding.Pcm8bit => 1,
            Encoding.Pcm16bit => 2,
            _ => 2
        };

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            if (_encoding == Encoding.Pcm24bitPacked)
                bps = 3;
            else if (_encoding == Encoding.Pcm32bit)
                bps = 4;
        }
        return frames * channels * bps;
    }

    public WaveFormat GetWaveFormat()
    {
        if (_audioRecord == null)
            throw new NullReferenceException("Audio record is null");

        return _waveFormat ??= new WaveFormat(int.Parse(playbackRepository.GetSettings().SelectedEncoding.Value), _audioRecord.ChannelCount, _audioRecord.SampleRate);
    }

    private static void OnVolumeChange(float gain)
    {
        _volume = gain;
    }

    private static Encoding MapToAndroidEncoding(int encoding)
    {
        return encoding switch
        {
            8 => Encoding.Pcm8bit,
            16 => Encoding.Pcm16bit,
            24 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm24bitPacked : throw new NotSupportedException("Pcm24bitPacked supported on sdk 31 or higher"),
            32 => OperatingSystem.IsAndroidVersionAtLeast(31) ? Encoding.Pcm32bit : throw new NotSupportedException("Pcm32bit supported on sdk 31 or higher"),
            _ => throw new NotSupportedException($"No such encoding supported: {encoding}")
        };
    }
}
