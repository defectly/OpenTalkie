using Android.Media;
using Android.Media.Projection;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;
using Encoding = Android.Media.Encoding;
using Microsoft.Maui.Storage;

namespace OpenTalkie.Platforms.Android.Common.Services;

public class PlaybackService(IPlaybackRepository playbackRepository, IScreenAudioCapturing audioRecording) : IPlaybackService
{
    private Encoding _encoding;
    private int _sampleRate;
    private ChannelOut _channelOut;

    private AudioRecord? _audioRecord;
    private WaveFormat? _waveFormat;

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is not created");

        return await _audioRecord.ReadAsync(buffer, offset, count);
    }

    private void LoadPreferences()
    {
        _encoding = (Encoding)Preferences.Get("PlaybackEncoding", (int)Encoding.Default);
        _sampleRate = Preferences.Get("PlaybackSampleRate", 48000);
        _channelOut = (ChannelOut)Preferences.Get("PlaybackChannelOut", (int)ChannelOut.Stereo);
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
}
