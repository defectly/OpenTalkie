using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Abstractions.Services;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Presentation.ViewModels;

namespace OpenTalkie.Presentation.ViewModels;

public partial class PlaybackStreamsViewModel(
    IMediator mediator,
    INavigationService navigationService,
    IUserDialogService dialogService,
    IEndpointCatalogService endpointCatalogService)
    : StreamEndpointsViewModelBase(mediator, navigationService, dialogService, endpointCatalogService, EndpointType.Playback)
{
}
