using Android.Media;
using NAudio.Wave;

namespace OpenTalkie.Platforms.Android;

public class AndroidAudioInput : IAudioInput
{
    private AudioRecord _audioRecord;
    private ProvideHelper _provideHelper;

    public int BufferSize { get; set; }

    public void SetAndroidOptions(int audioSource, int sampleRate, int channel, int encoding, int bufferSize)
    {
        BufferSize = bufferSize;

        if (_audioRecord.RecordingState == RecordState.Recording)
            _audioRecord.Stop();

        CreateAudioRecord((AudioSource)audioSource, sampleRate, (ChannelIn)channel, (Encoding)encoding, bufferSize);
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int count)
    {
        if (_audioRecord.RecordingState == RecordState.Stopped)
            _audioRecord.StartRecording();

        return await _audioRecord.ReadAsync(buffer, offset, count);
    }

    public IWaveProvider ToWaveProvider()
    {
        _provideHelper ??= new ProvideHelper(_audioRecord);

        return _provideHelper;
    }

    public ISampleProvider ToSampleProvider()
    {
        return ToWaveProvider().ToSampleProvider();
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _audioRecord.Stop();
        _audioRecord.Dispose();
        GC.SuppressFinalize(this);
    }

    private void CreateAudioRecord(AudioSource audioSource,
        int sampleRate, ChannelIn channel, Encoding encoding, int bufferSize)
    {
        _audioRecord = new(audioSource, sampleRate, channel, encoding, bufferSize);
    }
}
