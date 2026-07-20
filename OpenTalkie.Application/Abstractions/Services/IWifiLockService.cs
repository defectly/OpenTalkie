namespace OpenTalkie.Application.Abstractions.Services;

public interface IWifiLockService
{
    void Acquire();
    void Release();
}
