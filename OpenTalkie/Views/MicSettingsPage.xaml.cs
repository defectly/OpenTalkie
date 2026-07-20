using OpenTalkie.Presentation.ViewModels;
using System.Windows.Input;

namespace OpenTalkie.Presentation.Views;

public partial class MicSettingsPage : ContentPage
{
    private readonly MicSettingsViewModel _viewModel;

    public ICommand PacingToggledCommand => _viewModel.PacingToggledCommand;

    public MicSettingsPage(MicSettingsViewModel viewModel)
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
