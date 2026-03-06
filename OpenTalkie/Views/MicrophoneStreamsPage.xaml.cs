using Microsoft.Maui.Controls;
using OpenTalkie.Presentation.ViewModels;
using System.Windows.Input;

namespace OpenTalkie.Presentation.Views;

public partial class MicrophoneStreamsPage : ContentPage
{
    private readonly MicrophoneStreamsViewModel _viewModel;

    public ICommand StreamEnabledChangedCommand => _viewModel.StreamEnabledChangedCommand;
    public ICommand StreamVolumeChangedCommand => _viewModel.StreamVolumeChangedCommand;
    public ICommand ResetVolumeCommand => _viewModel.ResetVolumeCommand;
    public ICommand OpenSettingsCommand => _viewModel.OpenSettingsCommand;
    public ICommand DeleteStreamCommand => _viewModel.DeleteStreamCommand;

    public MicrophoneStreamsPage(MicrophoneStreamsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    private void OnStreamVolumeDragCompleted(object? sender, EventArgs e)
    {
        if (sender is not Slider { BindingContext: StreamEndpointItemViewModel endpoint })
        {
            return;
        }

        if (_viewModel.StreamVolumeChangedCommand.CanExecute(endpoint))
        {
            _viewModel.StreamVolumeChangedCommand.Execute(endpoint);
        }
    }
}
