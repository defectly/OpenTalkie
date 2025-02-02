using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenTalkie.ViewModels;

[QueryProperty(nameof(Name), "Name")]
[QueryProperty(nameof(Description), "Description")]
[QueryProperty(nameof(StreamName), "StreamName")]
[QueryProperty(nameof(Address), "Address")]
[QueryProperty(nameof(Port), "Port")]
public partial class StreamSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string _streamName;

    [ObservableProperty]
    private string _address;

    [ObservableProperty]
    private int _port;

    public StreamSettingsViewModel(string name, string description, string streamName, string address, int port)
    {
        Name = name;
        Description = description;
        StreamName = streamName;
        Address = address;
        Port = port;
    }

    public StreamSettingsViewModel()
    {
        
    }
}
