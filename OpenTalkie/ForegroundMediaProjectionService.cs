﻿#if ANDROID
using Intent = Android.Content.Intent;

namespace OpenTalkie;

internal partial class ForegroundMediaProjectionService
{
    private Intent _intent = new Intent(Android.App.Application.Context, typeof(ForegroundMediaProjectionService));

    public void Start() =>
        Android.App.Application.Context.StartForegroundService(_intent);

    public void Stop() =>
        Android.App.Application.Context.StopService(_intent);
}
#endif