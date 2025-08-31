using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.View;
using System.Collections.ObjectModel;

namespace OpenTalkie.ViewModel;
public class SettingsItem
{
    public required string Name { get; set; }
    public required string PageType { get; set; }
}
public partial class SettingsViewModel : ObservableObject
{
    private readonly MicSettingsPage _micSettingsPage;
    private readonly PlaybackSettingsPage _playbackSettingsPage;
    private readonly ReceiverSettingsPage _receiverSettingsPage;

    [ObservableProperty]
    private ObservableCollection<SettingsItem> settingsItems;

    public SettingsViewModel(MicSettingsPage micSettingsPage, PlaybackSettingsPage playbackSettingsPage, ReceiverSettingsPage receiverSettingsPage)
    {
        _micSettingsPage = micSettingsPage;
        _playbackSettingsPage = playbackSettingsPage;
        _receiverSettingsPage = receiverSettingsPage;

        SettingsItems = new ObservableCollection<SettingsItem>
        {
            new SettingsItem { Name = "Microphone Settings", PageType = "MicSettingsPage" },
            new SettingsItem { Name = "Receiver Settings", PageType = "ReceiverSettingsPage" },
        };

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            SettingsItems.Add(new SettingsItem { Name = "Cast Settings", PageType = "PlaybackSettingsPage" });
    }

    [RelayCommand]
    private async Task NavigateToSettingsPage(SettingsItem item)
    {
        if (item == null) return;

        Page? page = item.PageType switch
        {
            "MicSettingsPage" => _micSettingsPage,
            "PlaybackSettingsPage" => _playbackSettingsPage,
            "ReceiverSettingsPage" => _receiverSettingsPage,
            _ => null
        };

        if (page != null)
            await Shell.Current.Navigation.PushAsync(page);
    }
}
