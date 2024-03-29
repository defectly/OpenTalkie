using Intent = Android.Content.Intent;

namespace vOpenTalkie;

internal class ForegroundBatteryService
{
    private Intent _intent = new Intent(Android.App.Application.Context, typeof(ForegroundServiceDemo));

    public void Start() =>
        Android.App.Application.Context.StartForegroundService(_intent);

    public void Stop() =>
        Android.App.Application.Context.StopService(_intent);
}
