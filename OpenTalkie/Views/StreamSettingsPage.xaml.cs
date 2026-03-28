using Microsoft.Maui.Controls;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Presentation.ViewModels;
using System.Windows.Input;

namespace OpenTalkie.Presentation.Views;

public partial class StreamSettingsPage : ContentPage, IQueryAttributable
{
    private readonly StreamSettingsViewModel _viewModel;

    public ICommand DenoiseToggledCommand => _viewModel.DenoiseToggledCommand;
    public ICommand MobileDataToggledCommand => _viewModel.MobileDataToggledCommand;

    public StreamSettingsPage(StreamSettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (TryGetGuid(query, "EndpointId", out var endpointId))
        {
            _viewModel.EndpointId = endpointId;
        }

        if (TryGetEndpointType(query, "StreamType", out var streamType))
        {
            _viewModel.StreamType = streamType;
        }
    }

    private static bool TryGetGuid(IDictionary<string, object> query, string key, out Guid value)
    {
        if (query.TryGetValue(key, out var rawValue))
        {
            if (rawValue is Guid guid)
            {
                value = guid;
                return true;
            }

            if (rawValue is string stringValue && Guid.TryParse(stringValue, out guid))
            {
                value = guid;
                return true;
            }
        }

        value = Guid.Empty;
        return false;
    }

    private static bool TryGetEndpointType(IDictionary<string, object> query, string key, out EndpointType value)
    {
        if (query.TryGetValue(key, out var rawValue))
        {
            if (rawValue is EndpointType endpointType)
            {
                value = endpointType;
                return true;
            }

            if (rawValue is string stringValue && Enum.TryParse(stringValue, true, out endpointType))
            {
                value = endpointType;
                return true;
            }
        }

        value = default;
        return false;
    }
}
