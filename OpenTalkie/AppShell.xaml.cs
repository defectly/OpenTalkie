using OpenTalkie.View;

namespace OpenTalkie
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("StreamSettingsPage", typeof(StreamSettingsPage));
        }
    }
}
