using Android.Media;
using NAudio.Wave;

namespace OpenTalkie.Platforms.Android;

public class MicrophoneService : IMicrophoneService
{
    private readonly AndroidForegroundMicrophoneService _foregroundService;
    private AudioRecord? _audioRecord;
    private int _microphoneSource;
    private int _microphoneChannel;
    private int _microphoneSampleRate;
    private int _microphoneEncoding;
    public int BufferSize { get; set; }

    public MicrophoneService()
    {
        _foregroundService = new();
    }

    public void Start()
    {
        if (_audioRecord != null)
            return;

        LoadPreferences();
        CreateAudioRecord();

        if (_audioRecord.RecordingState == RecordState.Stopped)
        {
            _foregroundService.Start();
            _audioRecord.StartRecording();
        }
    }

    public void Stop()
    {
        if (_audioRecord == null)
            return;

        if (_audioRecord.RecordingState != RecordState.Stopped)
        {
            _audioRecord.Stop();
            _audioRecord.Dispose();
            _audioRecord = null;
            _foregroundService.Stop();
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _audioRecord.Read(buffer, offset, count);
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        return await _audioRecord.ReadAsync(buffer, offset, count);
    }

    public IWaveProvider ToWaveProvider()
    {
        var provideHelper = new ProvideHelper(_audioRecord);

        return provideHelper;
    }

    public ISampleProvider ToSampleProvider()
    {
        return ToWaveProvider().ToSampleProvider();
    }

    public void Dispose()
    {
        _audioRecord.Stop();
        _audioRecord.Dispose();
        _foregroundService.Stop();
        GC.SuppressFinalize(this);
    }

    private void LoadPreferences()
    {
        _microphoneSource = Preferences.Get("MicrophoneSource", (int)AudioSource.Default);
        _microphoneChannel = Preferences.Get("MicrophoneChannel", (int)ChannelIn.Default);
        _microphoneSampleRate = Preferences.Get("MicrophoneSampleRate", 48000);
        _microphoneEncoding = Preferences.Get("MicrophoneEncoding", (int)Encoding.Default);
        BufferSize = Preferences.Get("MicrophoneBufferSize", 1024);
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
