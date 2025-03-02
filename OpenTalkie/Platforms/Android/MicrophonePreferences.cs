using Android.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenTalkie;

public partial class MicrophonePreferencesRepository : ObservableObject, IMicrophonePreferencesRepository
{
    [ObservableProperty]
    private int _source;
    [ObservableProperty]
    private int _inputChannel;
    [ObservableProperty]
    private int _sampleRate;
    [ObservableProperty]
    private int _bitResolution;

    public MicrophonePreferencesRepository()
    {
        LoadPreferences();

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SavePreferences();
    }

    private void SavePreferences()
    {
        Preferences.Set("MicrophoneSource", Source);
        Preferences.Set("MicrophoneInputChannel", InputChannel);
        Preferences.Set("MicrophoneSampleRate", SampleRate);
        Preferences.Set("MicrophoneEncoding", BitResolution);
    }

    private void LoadPreferences()
    {
        Source = Preferences.Get("MicrophoneSource", (int)AudioSource.Default);
        InputChannel = Preferences.Get("MicrophoneInputChannel", (int)ChannelIn.Default);
        SampleRate = Preferences.Get("MicrophoneSampleRate", 48000);
        BitResolution = Preferences.Get("MicrophoneEncoding", (int)Encoding.Default);
    }
}
