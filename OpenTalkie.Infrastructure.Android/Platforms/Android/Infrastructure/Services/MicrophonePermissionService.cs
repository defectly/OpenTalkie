using OpenTalkie.Application.Abstractions.Services;

namespace OpenTalkie.Infrastructure.Android.Platforms.Android.Infrastructure.Services;

public sealed class MicrophonePermissionService : IMicrophonePermissionService
{
    public async Task<bool> RequestMicrophonePermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<Permissions.Microphone>();
        return status == PermissionStatus.Granted;
    }
}


