using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace OpenTalkie.ViewModels;

public partial class StreamCardViewModel : ObservableObject
{
    public StreamSettingsViewModel StreamSettings { get; set; }

    [ObservableProperty]
    private bool _enabled;

    public ICommand SwitchTurnedCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenCommand { get; }

    public StreamCardViewModel(StreamSettingsViewModel streamSettings, ICommand deleteCommand)
    {
        StreamSettings = streamSettings;
        Enabled = false;

        SwitchTurnedCommand = new Command(async () => await OnSwitchTurned());
        DeleteCommand = deleteCommand;
        OpenCommand = new Command(() => OpenStreamSettings());

    }

    public StreamCardViewModel()
    {

    }

    private async Task OnSwitchTurned()
    {
        Enabled = !Enabled;
    }

    private async void OpenStreamSettings()
    {
        var mainPage = Application.Current.MainPage;
        if (mainPage != null && mainPage is AppShell shell)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Name", StreamSettings.Name },
                { "Description", StreamSettings.Description },
                { "StreamName", StreamSettings.StreamName },
                { "Address", StreamSettings.Address },
                { "Port", StreamSettings.Port },
            };
            await Shell.Current.GoToAsync("streamsettings", navigationParameter);
        }
        else
        {
            // Manejo del caso en el que MainPage es nulo
            Console.WriteLine("MainPage es nulo o no es una instancia de AppShell");
        }
    }
}
