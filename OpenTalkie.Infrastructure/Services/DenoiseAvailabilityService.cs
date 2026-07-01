using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Infrastructure.RNNoise;

namespace OpenTalkie.Infrastructure.Services;

public sealed class DenoiseAvailabilityService : IDenoiseAvailabilityService
{
    private readonly Lock _sync = new();
    private readonly ILogger<DenoiseAvailabilityService> logger;
    private bool _initialized;
    private OperationResult _cachedResult;

    public DenoiseAvailabilityService(ILogger<DenoiseAvailabilityService> logger)
    {
        this.logger = logger;
    }

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
            var logLevel = _cachedResult.IsSuccess ? LogLevel.Information : LogLevel.Warning;
            if (logger.IsEnabled(logLevel))
            {
                logger.Log(logLevel, "RNNoise availability probe result: {Result}.", _cachedResult.IsSuccess
                    ? "available"
                    : _cachedResult.ErrorMessage);
            }

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
