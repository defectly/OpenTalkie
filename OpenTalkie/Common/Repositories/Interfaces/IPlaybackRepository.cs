﻿namespace OpenTalkie.Common.Repositories.Interfaces;

public interface IPlaybackRepository
{
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
}