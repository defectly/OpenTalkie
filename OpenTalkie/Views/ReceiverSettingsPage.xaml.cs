using OpenTalkie.Presentation.ViewModels;

namespace OpenTalkie.Presentation.Views;

public partial class ReceiverSettingsPage : ContentPage
{
    private readonly ReceiverSettingsViewModel _viewModel;

    public ReceiverSettingsPage(ReceiverSettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = _viewModel.ReloadStateAsync();
    }

    private void OnVolumeDragCompleted(object? sender, EventArgs e)
    {
        if (_viewModel.VolumeChangedCommand.CanExecute(null))
        {
            _viewModel.VolumeChangedCommand.Execute(null);
        }
    }
}
