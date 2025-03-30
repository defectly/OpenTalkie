namespace OpenTalkie.Common.Services.Interfaces;

public interface IMicrophoneService : IInputStream, IDisposable
{
    int BufferSize { get; set; }
    void Start();
    void Stop();
}