using Microsoft.Maui.Controls;
using OpenTalkie.Presentation.ViewModels;

namespace OpenTalkie.Presentation.Views;

public partial class PlaybackSettingsPage : ContentPage
{
    private readonly PlaybackSettingsViewModel _viewModel;

    public PlaybackSettingsPage(PlaybackSettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    private void OnVolumeDragCompleted(object? sender, EventArgs e)
    {
        if (_viewModel.VolumeChangedCommand.CanExecute(null))
        {
            _viewModel.VolumeChangedCommand.Execute(null);
        }
    }
}
