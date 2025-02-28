using Android.Media;
using NAudio.Wave;

namespace OpenTalkie.Platforms.Android;

public class MicrophoneService : IMicrophoneService
{
    private readonly AndroidForegroundMicrophoneService _foregroundService;
    private AudioRecord _audioRecord;
    private ProvideHelper _provideHelper;
    private int _microphoneSource;
    private int _microphoneChannel;
    private int _microphoneSampleRate;
    private int _microphoneEncoding;
    public int BufferSize { get; set; }

    public MicrophoneService()
    {
        _foregroundService = new();
        LoadPreferences();
    }

    private void LoadPreferences()
    {
        if (_audioRecord != null)
        {
            if (_audioRecord.RecordingState == RecordState.Recording)
            {
                _audioRecord.Stop();
                _audioRecord.Dispose();
                _foregroundService.Stop();
            }
        }

        _microphoneSource = Preferences.Get("MicrophoneSource", (int)AudioSource.Default);
        _microphoneChannel = Preferences.Get("MicrophoneChannel", (int)ChannelIn.Default);
        _microphoneSampleRate = Preferences.Get("MicrophoneSampleRate", 48000);
        _microphoneEncoding = Preferences.Get("MicrophoneEncoding", (int)Encoding.Default);
        BufferSize = Preferences.Get("MicrophoneBufferSize", 1024);

        _audioRecord =
            CreateAudioRecord
            (
                (AudioSource)_microphoneSource,
                _microphoneSampleRate,
                (ChannelIn)_microphoneChannel,
                (Encoding)_microphoneEncoding,
                BufferSize
            );
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_audioRecord.RecordingState == RecordState.Stopped)
        {
            _foregroundService.Start();
            _audioRecord.StartRecording();
        }

        return _audioRecord.Read(buffer, offset, count);
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord.RecordingState == RecordState.Stopped)
        {
            _foregroundService.Start();
            _audioRecord.StartRecording();
        }

        return await _audioRecord.ReadAsync(buffer, offset, count);
    }

    public IWaveProvider ToWaveProvider()
    {
        if (_audioRecord.RecordingState == RecordState.Stopped)
        {
            _foregroundService.Start();
            _audioRecord.StartRecording();
        }

        _provideHelper ??= new ProvideHelper(_audioRecord);

        return _provideHelper;
    }

    public ISampleProvider ToSampleProvider()
    {
        if (_audioRecord.RecordingState == RecordState.Stopped)
        {
            _foregroundService.Start();
            _audioRecord.StartRecording();
        }

        return ToWaveProvider().ToSampleProvider();
    }

    public List<(string Name, string Parameter)> GetPreferencesAsString()
    {
        List<(string Name, string Parameter)> values = [];

        values.Add(("MicrophoneSource", ((AudioSource)_microphoneSource).ToString()));
        values.Add(("MicrophoneChannel", ((ChannelIn)_microphoneChannel).ToString()));
        values.Add(("MicrophoneSampleRate", _microphoneSampleRate.ToString()));
        values.Add(("MicrophoneEncoding", ((Encoding)_microphoneEncoding).ToString()));
        values.Add(("MicrophoneBufferSize", BufferSize.ToString()));

        return values;
    }

    public void Dispose()
    {
        _audioRecord.Stop();
        _audioRecord.Dispose();
        _foregroundService.Stop();
        GC.SuppressFinalize(this);
    }

    private static AudioRecord CreateAudioRecord(AudioSource audioSource,
        int sampleRate, ChannelIn channel, Encoding encoding, int bufferSize)
    {
        return new(audioSource, sampleRate, channel, encoding, bufferSize);
    }
}
