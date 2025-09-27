namespace OpenTalkie.Common.Services.Interfaces;

public interface IWakeLockService
{
    void Acquire();
    void Release();
}