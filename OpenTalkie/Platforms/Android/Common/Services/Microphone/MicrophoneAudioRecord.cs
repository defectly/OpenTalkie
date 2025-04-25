using Android.Media;
using OpenTalkie.Common.Repositories.Interfaces;

namespace OpenTalkie.Platforms.Android.Common.Services.Microphone;

public static class MicrophoneAudioRecord
{
    private static float _volume;
    private static AudioRecord? _audioRecord;
    private static int _microphoneSource;
    private static int _microphoneChannel;
    private static int _microphoneSampleRate;
    private static int _microphoneEncoding;
    private static WaveFormat? _waveFormat;
    private static readonly IMicrophoneRepository microphoneRepository =
        IPlatformApplication.Current?.Services.GetService<IMicrophoneRepository>()
        ?? throw new NullReferenceException("Microphone repository not provided");
    public static int BufferSize { get; set; }

    static MicrophoneAudioRecord()
    {
        microphoneRepository.VolumeChanged += OnVolumeChange;
    }

    public static void Start()
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

        _audioRecord.StartRecording();
    }

    public static WaveFormat GetWaveFormat()
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is null");

        return _waveFormat ??=
            new WaveFormat(int.Parse(microphoneRepository.GetSelectedEncoding()), _audioRecord.ChannelCount, _audioRecord.SampleRate);
    }

    public static void Stop()
    {
        if (_audioRecord == null)
            return;

        if (_audioRecord.RecordingState == RecordState.Stopped)
            return;

        _audioRecord.Stop();
        _audioRecord.Release();
        _audioRecord.Dispose();
        _audioRecord = null;
        _waveFormat = null;
    }

    public static async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord == null)
            throw new NullReferenceException($"Audio record is null");

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

    private static void LoadPreferences()
    {
        _microphoneSource = Preferences.Get("MicrophoneSource", (int)AudioSource.Default);
        _microphoneChannel = Preferences.Get("MicrophoneInputChannel", (int)ChannelIn.Default);
        _microphoneSampleRate = Preferences.Get("MicrophoneSampleRate", 48000);
        _microphoneEncoding = Preferences.Get("MicrophoneEncoding", (int)Encoding.Default);
        BufferSize = Preferences.Get("MicrophoneBufferSize", 960);
        _volume = Preferences.Get("MicrophoneVolume", 1f);
    }

    private static void CreateAudioRecord()
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

    private static void CreateAudioRecord(AudioSource audioSource,
        int sampleRate, ChannelIn channel, Encoding encoding, int bufferSize)
    {
        _audioRecord = new(audioSource, sampleRate, channel, encoding, bufferSize);
    }

    private static void OnVolumeChange(float gain)
    {
        _volume = gain;
    }
}
