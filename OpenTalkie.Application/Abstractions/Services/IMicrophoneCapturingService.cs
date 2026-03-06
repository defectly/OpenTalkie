namespace OpenTalkie.Application.Abstractions.Services;

public interface IMicrophoneCapturingService : IInputStream
{
    Action<bool>? OnServiceStateChange { get; set; }
    Task StartAsync();
    void Stop();
    int GetBufferSize();
}
