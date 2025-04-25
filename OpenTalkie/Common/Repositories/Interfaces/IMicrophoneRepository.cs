namespace OpenTalkie.Common.Repositories.Interfaces;

public interface IMicrophoneRepository
{
    Action<float> VolumeChanged { get; set; }
    List<string> GetAudioSources();
    List<string> GetInputChannels();
    List<string> GetSampleRates();
    List<string> GetEncodings();
    string GetSelectedSource();
    string GetSelectedInputChannel();
    string GetSelectedSampleRate();
    string GetSelectedEncoding();
    string GetSelectedBufferSize();
    float GetSelectedVolume();
    void SetSelectedBufferSize(string bufferSize);
    void SetSelectedSource(string source);
    void SetSelectedInputChannel(string inputChannel);
    void SetSelectedSampleRate(string sampleRate);
    void SetSelectedEncoding(string encoding);
    void SetSelectedVolume(float gain);
}