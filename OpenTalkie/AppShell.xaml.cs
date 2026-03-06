using Microsoft.Maui.Controls;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Presentation.Views;

namespace OpenTalkie;

public partial class AppShell : Shell
{
    public AppShell(IPlatformCapabilitiesService platformCapabilitiesService)
    {
        InitializeComponent();

        if (!platformCapabilitiesService.GetCapabilities().IsPlaybackCaptureSupported)
            PlaybackStreams.IsVisible = false;

        Routing.RegisterRoute("StreamSettingsPage", typeof(StreamSettingsPage));
        Routing.RegisterRoute("AddStreamPage", typeof(AddStreamPage));
        Routing.RegisterRoute("MicSettingsPage", typeof(MicSettingsPage));
        Routing.RegisterRoute("PlaybackSettingsPage", typeof(PlaybackSettingsPage));
        Routing.RegisterRoute("ReceiverSettingsPage", typeof(ReceiverSettingsPage));
    }
}
