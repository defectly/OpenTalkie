using CommunityToolkit.Maui.Views;
using OpenTalkie.View.Popups;

namespace OpenTalkie;

public partial class App : Application
{
    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        MainPage = serviceProvider.GetService<AppShell>();

        AppDomain.CurrentDomain.UnhandledException += async (send, error) =>
        {
            var errorPopup = new ErrorPopup(error.ExceptionObject.ToString());
            await MainPage.ShowPopupAsync(errorPopup);
        };
    }
}
