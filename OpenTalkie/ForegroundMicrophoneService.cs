#if ANDROID
using Intent = Android.Content.Intent;

namespace OpenTalkie;

internal partial class ForegroundMicrophoneService
{
    private Intent _intent = new Intent(Android.App.Application.Context, typeof(ForegroundMicrophoneService));

    public void Start() =>
        Android.App.Application.Context.StartForegroundService(_intent);

    public void Stop() =>
        Android.App.Application.Context.StopService(_intent);
}
#endif