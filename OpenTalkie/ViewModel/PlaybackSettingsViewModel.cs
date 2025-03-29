using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Repositories.Interfaces;
using OpenTalkie.View.Popups;

namespace OpenTalkie.ViewModel;

public partial class PlaybackSettingsViewModel : ObservableObject
{
    private readonly IPlaybackRepository _playbackRepository;
    private readonly AppShell _mainPage;

    [ObservableProperty]
    private string selectedChannelOut;

    [ObservableProperty]
    private string selectedEncoding;

    [ObservableProperty]
    private string selectedSampleRate;

    [ObservableProperty]
    private string selectedBufferSize;

    public PlaybackSettingsViewModel(AppShell mainPage, IPlaybackRepository playbackRepository)
    {
        _mainPage = mainPage;
        _playbackRepository = playbackRepository;

        SelectedChannelOut = _playbackRepository.GetSelectedChannelOut();
        SelectedEncoding = _playbackRepository.GetSelectedEncoding();
        SelectedSampleRate = _playbackRepository.GetSelectedSampleRate();
        SelectedBufferSize = _playbackRepository.GetSelectedBufferSize();
    }

    [RelayCommand]
    private async Task EditField(string fieldName)
    {
        string[] options = fieldName switch
        {
            "ChannelOut" => _playbackRepository.GetOutputChannels().ToArray(),
            "SampleRate" => _playbackRepository.GetSampleRates().ToArray(),
            "Encoding" => _playbackRepository.GetEncodings().ToArray(),
            _ => []
        };

        string currentValue = fieldName switch
        {
            "ChannelOut" => SelectedChannelOut,
            "SampleRate" => SelectedSampleRate,
            "Encoding" => SelectedEncoding,
            _ => ""
        };

        var popup = new OptionsPopup(
            $"Choose {fieldName}",
            options,
            (result) =>
            {
                if (result != null && result != currentValue)
                {
                    switch (fieldName)
                    {
                        case "ChannelOut":
                            SelectedChannelOut = result;
                            _playbackRepository.SetSelectedChannelOut(result);
                            break;
                        case "SampleRate":
                            SelectedSampleRate = result;
                            _playbackRepository.SetSelectedSampleRate(result);
                            break;
                        case "Encoding":
                            SelectedEncoding = result;
                            _playbackRepository.SetSelectedEncoding(result);
                            break;
                    }
                }
            });

        await _mainPage.ShowPopupAsync(popup);
    }

    [RelayCommand]
    private async Task EditBufferSize()
    {
        string currentValue = SelectedBufferSize ?? _playbackRepository.GetSelectedBufferSize();

        var popup = new EditFieldPopup(
            "Change buffer size",
            currentValue,
            Keyboard.Numeric,
            async (result) =>
            {
                if (result != null && int.TryParse(result, out int bufferSize) && bufferSize > 0)
                {
                    SelectedBufferSize = result;
                    _playbackRepository.SetSelectedBufferSize(result);
                }
                else if (result != null)
                {
                    var errorPopup = new ErrorPopup("Something's wrong with the number");
                    await _mainPage.ShowPopupAsync(errorPopup);
                }
            });

        await _mainPage.ShowPopupAsync(popup);
    }
}