using Microsoft.Maui.Controls;
using OpenTalkie.Presentation.ViewModels;
using System.Windows.Input;

namespace OpenTalkie.Presentation.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    public ICommand NavigateToSettingsPageCommand => _viewModel.NavigateToSettingsPageCommand;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }
}
