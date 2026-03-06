using Microsoft.Maui.Controls;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Presentation.ViewModels;

namespace OpenTalkie.Presentation.Views;

public partial class AddStreamPage : ContentPage, IQueryAttributable
{
    private readonly AddStreamViewModel _viewModel;

    public AddStreamPage(AddStreamViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("StreamType", out var rawValue))
        {
            if (rawValue is EndpointType endpointType)
            {
                _viewModel.StreamType = endpointType;
            }
            else if (rawValue is string stringValue && Enum.TryParse<EndpointType>(stringValue, true, out endpointType))
            {
                _viewModel.StreamType = endpointType;
            }
        }
    }
}
