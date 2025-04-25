namespace OpenTalkie.Common.Repositories.Interfaces;

public interface IPlaybackRepository
{
    Action<float> VolumeChanged { get; set; }
    List<string> GetSampleRates();
    List<string> GetOutputChannels();
    List<string> GetEncodings();
    string GetSelectedBufferSize();
    string GetSelectedChannelOut();
    string GetSelectedEncoding();
    string GetSelectedSampleRate();
    void SetSelectedChannelOut(string encoding);
    void SetSelectedEncoding(string encoding);
    void SetSelectedSampleRate(string sampleRate);
    void SetSelectedBufferSize(string bufferSize);
    float GetSelectedVolume();
    void SetSelectedVolume(float gain);
}