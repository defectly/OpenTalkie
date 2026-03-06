using CommunityToolkit.Maui.Views;
using OpenTalkie.Presentation.ViewModels.Popups;
using System.Windows.Input;

namespace OpenTalkie.Presentation.Views.Popups;

public partial class OptionsPopup : Popup
{
    private readonly OptionsViewModel _viewModel;
    public ICommand SelectOptionCommand => _viewModel.SelectOptionCommand;

    public OptionsPopup(string title, string[] options, Action<string> onSelect)
    {
        InitializeComponent();
        _viewModel = new OptionsViewModel(title, options, onSelect, this);
        BindingContext = _viewModel;
    }
}
