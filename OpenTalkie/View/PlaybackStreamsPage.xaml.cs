using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class PlaybackStreamsPage : ContentPage
{
    public PlaybackStreamsPage(PlaybackStreamsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}