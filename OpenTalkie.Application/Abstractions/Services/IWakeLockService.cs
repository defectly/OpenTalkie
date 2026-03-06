namespace OpenTalkie.Application.Abstractions.Services;

public interface IWakeLockService
{
    void Acquire();
    void Release();
}
