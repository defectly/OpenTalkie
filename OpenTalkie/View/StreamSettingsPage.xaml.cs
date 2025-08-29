using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

[QueryProperty(nameof(Endpoint), "Endpoint")]
public partial class StreamSettingsPage : ContentPage
{
    private readonly StreamSettingsViewModel _viewModel;

    public Endpoint Endpoint
    {
        set
        {
            _viewModel.Endpoint = value;
        }
    }

    public StreamSettingsPage(StreamSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}
