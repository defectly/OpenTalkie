using OpenTalkie.Application.Models;

namespace OpenTalkie.Application.Abstractions.Services;

public interface IScreenAudioCapturing
{
    bool IsSupported { get; }
    Task<bool> RequestCaptureAsync(ScreenAudioCapturingOptions? options = null);
    void StopCapture();
}
