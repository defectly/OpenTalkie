using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenTalkie.ViewModel;

public partial class StreamsViewModel : ObservableObject
{
    [ObservableProperty]
    private MicrophoneStreamsViewModel microphoneStreamsViewModel;

    [ObservableProperty]
    private PlaybackStreamsViewModel playbackStreamsViewModel;

    [ObservableProperty]
    private bool isMicrophoneTabSelected;

    [ObservableProperty]
    private bool isPlaybackTabSelected;

    public StreamsViewModel(MicrophoneStreamsViewModel microphoneVM, PlaybackStreamsViewModel playbackVM)
    {
        MicrophoneStreamsViewModel = microphoneVM;
        PlaybackStreamsViewModel = playbackVM;
        ShowMicrophoneStreams(); // По умолчанию
        Console.WriteLine("StreamsViewModel initialized");
    }

    [RelayCommand]
    public void ShowMicrophoneStreams()
    {
        IsMicrophoneTabSelected = true;
        IsPlaybackTabSelected = false;
        Console.WriteLine($"Showing Microphone Streams: IsMicrophoneTabSelected={IsMicrophoneTabSelected}, IsPlaybackTabSelected={IsPlaybackTabSelected}");
    }

    [RelayCommand]
    public void ShowPlaybackStreams()
    {
        IsMicrophoneTabSelected = false;
        IsPlaybackTabSelected = true;
        Console.WriteLine($"Showing Playback Streams: IsMicrophoneTabSelected={IsMicrophoneTabSelected}, IsPlaybackTabSelected={IsPlaybackTabSelected}");
    }
}