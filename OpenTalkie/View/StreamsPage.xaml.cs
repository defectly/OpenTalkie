using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class StreamsPage : ContentPage
{
	public StreamsPage(StreamsViewModel vm)
    {
		InitializeComponent();
        BindingContext = vm;
    }
}