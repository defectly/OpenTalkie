using Android.Media;
using NAudio.Wave;
using OpenTalkie.Common.Services.Interfaces;
using Encoding = Android.Media.Encoding;

namespace OpenTalkie.Platforms.Android;

public class PlaybackService : IPlaybackService
{
    private Encoding _encoding;
    private int _sampleRate;
    private ChannelOut _channelOut;

    private readonly ForegroundMediaProjectionService _foregroundService = new();
    private AudioRecord? _audioRecord;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is not created");

        return _audioRecord.Read(buffer, offset, count);
    }

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
        _channelOut = (ChannelOut)Preferences.Get("ChannelOut", (int)ChannelOut.Default);
    }

    public void Start()
    {
        if (_audioRecord != null)
            return;

        LoadPreferences();
        CreateAudioRecord();

        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record not created");

        if (_audioRecord.RecordingState != RecordState.Stopped)
            return;

        _foregroundService.Start();
        _audioRecord.StartRecording();
    }

    private void CreateAudioRecord()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            throw new NotSupportedException($"Minimum android version is 10 (SDK 29)");

        var config = new AudioPlaybackCaptureConfiguration.Builder(SystemAudioCaptureCallback.MediaProjection)
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
        _foregroundService.Stop();
    }

    public ISampleProvider ToSampleProvider()
    {
        return ToWaveProvider().ToSampleProvider();
    }

    public IWaveProvider ToWaveProvider()
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Can't convert, audio record is null");

        var provideHelper = new ProvideHelper(_audioRecord);

        return provideHelper;
    }
}
