﻿using Android.Media;
using Android.Media.Projection;
using NAudio.Wave;
using OpenTalkie.Common.Services.Interfaces;
using Encoding = Android.Media.Encoding;

namespace OpenTalkie.Platforms.Android.Common.Services;

public class PlaybackService : IPlaybackService
{
    private Encoding _encoding;
    private int _sampleRate;
    private ChannelOut _channelOut;

    private AudioRecord? _audioRecord;
    private MediaProjectionProvider _mediaProjectionProvider;
    private WaveFormat? _waveFormat;

    public PlaybackService()
    {
        var mediaProjectionProvider = ((MainActivity)Platform.CurrentActivity!).MediaProjectionProvider;

        if (mediaProjectionProvider == null)
            throw new NullReferenceException($"Media projection provider is null");

        _mediaProjectionProvider = mediaProjectionProvider;
    }

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
        _channelOut = (ChannelOut)Preferences.Get("PlaybackChannelOut", (int)ChannelOut.Stereo);
    }

    public void Start()
    {
        if (_audioRecord != null)
            return;

        var mediaProjection = _mediaProjectionProvider.GetMediaProjection();

        if (mediaProjection == null)
            throw new NullReferenceException($"Media projection not provided");

        LoadPreferences();
        CreateAudioRecord(mediaProjection);

        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record not created");

        _audioRecord.StartRecording();

        return;
    }

    public async Task<bool> RequestPermissionAsync()
    {
        return await _mediaProjectionProvider.RequestMediaProjectionPermissionAsync();
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
        _mediaProjectionProvider.DisposeMediaProjection();
        _waveFormat = null;
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

    public int GetBufferSize()
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is not created");

        return _audioRecord.BufferSizeInFrames;
    }

    public WaveFormat GetWaveFormat()
    {
        return _waveFormat ??= new WaveFormat(_audioRecord.SampleRate, 16, _audioRecord.ChannelCount);
    }
}
