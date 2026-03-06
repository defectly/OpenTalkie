using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;

namespace OpenTalkie.Application.Streams.Commands;

public readonly record struct DeleteStreamEndpointCommand(EndpointType StreamType, Guid EndpointId)
    : ICommand<OperationResult>;

public sealed class DeleteStreamEndpointCommandHandler(
    IEndpointCatalogService endpointCatalogService,
    IEndpointRepository endpointRepository)
    : ICommandHandler<DeleteStreamEndpointCommand, OperationResult>
{
    public async ValueTask<OperationResult> Handle(
        DeleteStreamEndpointCommand command,
        CancellationToken cancellationToken)
    {
        var endpoint = endpointCatalogService.GetEndpoint(command.StreamType, command.EndpointId);
        if (endpoint == null)
        {
            return OperationResult.Fail("Stream endpoint was not found.");
        }

        try
        {
            await endpointRepository.RemoveAsync(endpoint.Id);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Failed to persist stream endpoint removal: {ex.Message}");
        }

        endpointCatalogService.RemoveEndpoint(command.StreamType, endpoint.Id);
        return OperationResult.Success();
    }
}
