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

    [ObservableProperty]
    private ObservableCollection<SettingsItem> settingsItems;

    public SettingsViewModel(MicSettingsPage micSettingsPage, PlaybackSettingsPage playbackSettingsPage)
    {
        _micSettingsPage = micSettingsPage;
        _playbackSettingsPage = playbackSettingsPage;

        SettingsItems = new ObservableCollection<SettingsItem>
        {
            new SettingsItem { Name = "Microphone Settings", PageType = "MicSettingsPage" },
            new SettingsItem { Name = "Playback Settings", PageType = "PlaybackSettingsPage" }
        };
    }

    [RelayCommand]
    private async Task NavigateToSettingsPage(SettingsItem item)
    {
        if (item == null) return;

        Page? page = item.PageType switch
        {
            "MicSettingsPage" => _micSettingsPage,
            "PlaybackSettingsPage" => _playbackSettingsPage,
            _ => null
        };

        if (page != null)
            await Shell.Current.Navigation.PushAsync(page);
    }
}