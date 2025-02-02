using OpenTalkie.Views;

namespace OpenTalkie
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("streamcard/streamsettings", typeof(StreamSettingsView));
        }
    }
}
