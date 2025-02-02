using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OpenTalkie.ViewModels;

public partial class StreamsViewModel
{
    public ObservableCollection<StreamCardViewModel> Cards { get; set; }

    public ICommand DeleteCardCommand { get; }

    public StreamsViewModel()
    {
        DeleteCardCommand = new RelayCommand<StreamCardViewModel>(OnCardDelete);

        Cards =
        [
            new(new("firstStream", "eeeeeeee", "myStremName!", "1.1.1.1", 100), DeleteCardCommand),
            new(new("firstStream", "eeeeeeee", "", "", 1), DeleteCardCommand),
            new(new("firstStream", "eeeeeeee", "", "", 1), DeleteCardCommand),
        ];
    }

    private void OnCardDelete(StreamCardViewModel card)
    {
        card.Enabled = false;
        Cards.Remove(card);
    }
}