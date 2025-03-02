using System.ComponentModel;

namespace OpenTalkie;

public interface IMicrophonePreferencesRepository : INotifyPropertyChanged
{
    int Source { get; set; }
    int InputChannel { get; set; }
    int SampleRate { get; set; }
    int BitResolution { get; set; }
}
