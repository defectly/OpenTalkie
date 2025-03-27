namespace OpenTalkie.Common.Repositories.Interfaces;

public interface IPlaybackRepository
{
    string GetSelectedChannelOut();
    string GetSelectedEncoding();
    string GetSelectedSampleRate();
    void SetSelectedChannelOut(string encoding);
    void SetSelectedEncoding(string encoding);
    void SetSelectedSampleRate(string sampleRate);
}