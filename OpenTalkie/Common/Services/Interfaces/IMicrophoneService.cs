using NAudio.Wave;

namespace OpenTalkie.Common.Services.Interfaces;

public interface IMicrophoneService : IInputStream, IDisposable
{
    int BufferSize { get; set; }
    int Read(byte[] buffer, int offset, int count);
    ISampleProvider ToSampleProvider();
    IWaveProvider ToWaveProvider();
    void Start();
    void Stop();
}