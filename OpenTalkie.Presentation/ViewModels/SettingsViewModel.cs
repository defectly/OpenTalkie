using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Presentation.Abstractions.Services;
using System.Collections.ObjectModel;

namespace OpenTalkie.Presentation.ViewModels;

public class SettingsItem
{
    public required string Name { get; set; }
    public required string Route { get; set; }
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    public partial ObservableCollection<SettingsItem> SettingsItems { get; set; }

    public SettingsViewModel(INavigationService navigationService, IPlatformCapabilitiesService platformCapabilitiesService)
    {
        _navigationService = navigationService;

        SettingsItems = new ObservableCollection<SettingsItem>
        {
            new SettingsItem { Name = "Microphone Settings", Route = "MicSettingsPage" },
            new SettingsItem { Name = "Receiver Settings", Route = "ReceiverSettingsPage" },
        };

        if (platformCapabilitiesService.GetCapabilities().IsPlaybackCaptureSupported)
            SettingsItems.Add(new SettingsItem { Name = "Cast Settings", Route = "PlaybackSettingsPage" });
    }

    [RelayCommand]
    private async Task NavigateToSettingsPage(SettingsItem item)
    {
        if (item == null) return;

        await _navigationService.NavigateToAsync(item.Route);
    }
}
