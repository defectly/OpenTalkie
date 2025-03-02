namespace OpenTalkie;

public interface IMicrophoneRepository
{
    List<string> GetAudioSources();
    List<string> GetInputChannels();
    List<string> GetSampleRates();
    List<string> GetEncodings();
    string GetSelectedSource();
    string GetSelectedInputChannel();
    string GetSelectedSampleRate();
    string GetSelectedEncoding();
    void SetSelectedSource(string source);
    void SetSelectedInputChannel(string inputChannel);
    void SetSelectedSampleRate(string sampleRate);
    void SetSelectedEncoding(string encoding);
}