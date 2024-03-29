using Intent = Android.Content.Intent;

namespace vOpenTalkie;

internal partial class ForegroundBatteryService
{
    private Intent _intent = new Intent(Android.App.Application.Context, typeof(ForegroundBatteryService));

    public void Start() =>
        Android.App.Application.Context.StartForegroundService(_intent);

    public void Stop() =>
        Android.App.Application.Context.StopService(_intent);
}
