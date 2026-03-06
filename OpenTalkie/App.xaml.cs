using Microsoft.Extensions.DependencyInjection;
using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();

        AppDomain.CurrentDomain.UnhandledException += async (send, error) =>
        {
            var errorMessage = error.ExceptionObject?.ToString() ?? "Unhandled exception occurred.";
            IUserDialogService? dialogService = _serviceProvider.GetService<IUserDialogService>();
            if (dialogService != null)
            {
                await dialogService.ShowErrorAsync(errorMessage);
            }
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_serviceProvider.GetRequiredService<AppShell>());
    }
}
