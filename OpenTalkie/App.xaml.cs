using OpenTalkie.View;

namespace OpenTalkie;

public partial class App : Application
{
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        MainPage = serviceProvider.GetService<AppShell>();
    }
}
