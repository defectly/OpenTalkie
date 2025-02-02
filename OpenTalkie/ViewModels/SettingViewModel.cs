using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace OpenTalkie.ViewModels;

public partial class SettingViewModel : ObservableObject
{
    public List<string> Values { get; set; }
    public string? IconPath { get; set; }
    public string Name { get; set; }

    [ObservableProperty]
    private string? _selectedValue;

    public ICommand SettingTappedCommand { get; }

    public SettingViewModel(string name, List<string> values, string? selectedValue = null, string? iconPath = null)
    {
        Name = name;
        Values = values;
        SelectedValue = selectedValue;
        IconPath = iconPath;

        SettingTappedCommand = new Command(async () => await OnSettingTapped());
    }

    public SettingViewModel()
    {
        
    }

    private async Task OnSettingTapped()
    {
        SelectedValue = await Application.Current.MainPage
            .DisplayActionSheet("Select Parameter", "Cancel", null, Values.ToArray());
    }
}