using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

[QueryProperty(nameof(Endpoint), "Endpoint")]
public partial class StreamSettingsPage : ContentPage
{
    private readonly StreamSettingsViewModel _vm;

    public Endpoint Endpoint
    {
        set
        {
            _vm.Endpoint = value;
        }
    }

    public StreamSettingsPage(StreamSettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }
}