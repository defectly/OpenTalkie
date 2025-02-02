using System.Collections.ObjectModel;

namespace OpenTalkie.ViewModels;

public class MicrophoneViewModel
{
    public ObservableCollection<SettingViewModel> Settings { get; set; } =
    [
        new("SampleRate", ["48000", "44100"], "48000"),
        new("ChannelType", ["Stereo", "Mono"], "Stereo"),
        new("MicrophoneType", ["Unprocessed", "Camcorder"], "Unprocessed"),
    ];
}
