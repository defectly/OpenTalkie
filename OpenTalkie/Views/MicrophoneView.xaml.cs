using OpenTalkie.ViewModels;

namespace OpenTalkie.Views;

public partial class MicrophoneView : ContentPage
{
    public MicrophoneView(MicrophoneViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}