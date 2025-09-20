namespace OpenTalkie.Common.Services.Interfaces;

public interface IMicrophoneCapturingService : IInputStream
{
    Action<bool>? OnServiceStateChange { get; set; }
    Task StartAsync();
    void Stop();
    int GetBufferSize();
}