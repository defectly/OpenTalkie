﻿using Android.Media;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.Common.Services.Interfaces;
using OpenTalkie.Platforms.Android.Common.ForegroundServices;
using Microsoft.Maui.Storage;

namespace OpenTalkie.Platforms.Android.Common.Services;

public class MicrophoneService(IMicrophoneRepository microphoneRepository) : IMicrophoneService
{
    private readonly MicrophoneForegroundService _foregroundService = new();
    private AudioRecord? _audioRecord;
    private int _microphoneSource;
    private int _microphoneChannel;
    private int _microphoneSampleRate;
    private int _microphoneEncoding;
    private WaveFormat? _waveFormat;
    public int BufferSize { get; set; }

    public void Start()
    {
        if (_audioRecord != null)
            return;

        LoadPreferences();
        CreateAudioRecord();

        if (_audioRecord == null || _audioRecord.State == State.Uninitialized)
        {
            _audioRecord = null;
            throw new Exception("Can't initialize audio record.. Selected parameters may be not supported");
        }

        if (_audioRecord.RecordingState == RecordState.Stopped)
        {
            _foregroundService.Start();
            _audioRecord.StartRecording();
        }
    }

    public WaveFormat GetWaveFormat()
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is null");

        return _waveFormat ??= 
            new WaveFormat(int.Parse(microphoneRepository.GetSelectedEncoding()), _audioRecord.ChannelCount, _audioRecord.SampleRate);
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
        _waveFormat = null;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is null");

        return await _audioRecord.ReadAsync(buffer, offset, count);
    }

    public void Dispose()
    {
        _audioRecord?.Stop();
        _audioRecord?.Dispose();
        _foregroundService.Stop();
        GC.SuppressFinalize(this);
    }

    private void LoadPreferences()
    {
        _microphoneSource = Preferences.Get("MicrophoneSource", (int)AudioSource.Default);
        _microphoneChannel = Preferences.Get("MicrophoneInputChannel", (int)ChannelIn.Default);
        _microphoneSampleRate = Preferences.Get("MicrophoneSampleRate", 48000);
        _microphoneEncoding = Preferences.Get("MicrophoneEncoding", (int)Encoding.Default);
        BufferSize = Preferences.Get("MicrophoneBufferSize", 960);
    }

    private void CreateAudioRecord()
    {
        CreateAudioRecord
            (
                (AudioSource)_microphoneSource,
                _microphoneSampleRate,
                (ChannelIn)_microphoneChannel,
                (Encoding)_microphoneEncoding,
                BufferSize
            );
    }

    private void CreateAudioRecord(AudioSource audioSource,
        int sampleRate, ChannelIn channel, Encoding encoding, int bufferSize)
    {
        _audioRecord = new(audioSource, sampleRate, channel, encoding, bufferSize);
    }
}
