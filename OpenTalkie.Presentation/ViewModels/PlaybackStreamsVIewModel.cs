using Mediator;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Presentation.Abstractions.Services;

namespace OpenTalkie.Presentation.ViewModels;

public partial class PlaybackStreamsViewModel(
    IMediator mediator,
    INavigationService navigationService,
    IUserDialogService dialogService,
    IEndpointCatalogService endpointCatalogService)
    : StreamEndpointsViewModelBase(mediator, navigationService, dialogService, endpointCatalogService, EndpointType.Playback)
{
}
