using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTalkie.Common.Repositories.Interfaces;

namespace OpenTalkie.ViewModel;

public partial class ReceiverSettingsViewModel : ObservableObject
{
    private readonly IReceiverRepository _receiverRepository;

    [ObservableProperty]
    private float volume;

    public ReceiverSettingsViewModel(IReceiverRepository receiverRepository)
    {
        _receiverRepository = receiverRepository;
        Volume = _receiverRepository.GetSelectedVolume() * 100f;
    }

    [RelayCommand]
    public void VolumeChanged()
    {
        _receiverRepository.SetSelectedVolume(Volume / 100.0f);
    }

    [RelayCommand]
    private void ResetVolume()
    {
        Volume = 100.0f;
        _receiverRepository.SetSelectedVolume(Volume / 100.0f);
    }
}

