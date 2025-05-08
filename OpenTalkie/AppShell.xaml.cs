using OpenTalkie.View;

namespace OpenTalkie;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            PlaybackStreams.IsVisible = false;

        Routing.RegisterRoute("StreamSettingsPage", typeof(StreamSettingsPage));
        Routing.RegisterRoute("AddStreamPage", typeof(AddStreamPage));
    }
}
