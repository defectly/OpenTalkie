using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Infrastructure.RNNoise;

namespace OpenTalkie.Infrastructure.Services;

public sealed class DenoiseAvailabilityService : IDenoiseAvailabilityService
{
    private readonly object _sync = new();
    private bool _initialized;
    private OperationResult _cachedResult;

    public OperationResult CheckAvailability()
    {
        if (_initialized)
            return _cachedResult;

        lock (_sync)
        {
            if (_initialized)
                return _cachedResult;

            _cachedResult = Probe();
            _initialized = true;
            return _cachedResult;
        }
    }

    private static OperationResult Probe()
    {
        try
        {
            using var dn = new Denoiser();
            return OperationResult.Success();
        }
        catch (DllNotFoundException)
        {
            return OperationResult.Fail("RNNoise native library is unavailable on this device/build (possibly incompatible native binary/page-size).");
        }
        catch (BadImageFormatException)
        {
            return OperationResult.Fail("RNNoise binary is incompatible with this device ABI.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"RNNoise initialization failed: {ex.Message}");
        }
    }
}
