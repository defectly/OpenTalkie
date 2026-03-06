namespace OpenTalkie.Application.Abstractions.Services;

public interface IMicrophonePermissionService
{
    Task<bool> RequestMicrophonePermissionAsync();
}

