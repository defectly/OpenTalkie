using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class MicrophoneStreamsPage : ContentPage
{
    public MicrophoneStreamsPage(MicrophoneStreamsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}