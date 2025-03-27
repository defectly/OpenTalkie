using OpenTalkie.ViewModel;

namespace OpenTalkie.View;

public partial class StreamsPage : ContentPage
{
    private readonly StreamsViewModel viewModel;

    public StreamsPage(StreamsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        Console.WriteLine($"StreamsPage BindingContext set: {viewModel != null}");
    }

    private void OnTabCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (sender is RadioButton radioButton && e.Value)
        {
            if (radioButton.Content.ToString() == "Microphone Streams")
            {
                viewModel.ShowMicrophoneStreams();
            }
            else if (radioButton.Content.ToString() == "Playback Streams")
            {
                viewModel.ShowPlaybackStreams();
            }
        }
    }
}