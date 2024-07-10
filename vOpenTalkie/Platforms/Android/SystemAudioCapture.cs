using Android.Media.Projection;

namespace vOpenTalkie.Platforms.Android;

public class SystemAudioCapture
{
    public SystemAudioCaptureCallback SystemAudioCaptureCallback = MainActivity.SystemAudioCaptureCallback;
    public SystemAudioCapture()
    {
        SystemAudioCaptureCallback.Running += OnRunningChange;
    }

    private void OnRunningChange(bool obj)
    {
        Running = obj;
    }

    public bool Running { get; private set; } = false;

    public void Start()
    {
        if (!Running)
            MainActivity.MediaProjectionLauncher.Launch(SystemAudioCaptureCallback.MediaProjectionManager.CreateScreenCaptureIntent());



        if (SystemAudioCaptureCallback.ResultCode == -1)
            Running = true;
    }

    public void Stop()
    {
        if (Running)
            SystemAudioCaptureCallback.MediaProjection.Stop();
        SystemAudioCaptureCallback.ResultCode = 100;
        Running = false;
    }
}
