using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.VBAN;

namespace OpenTalkie.Application.Streams.Commands;

public readonly record struct CreateStreamEndpointCommand(
    EndpointType StreamType,
    string Name,
    string Hostname,
    int Port,
    bool IsDenoiseEnabled,
    bool AllowMobileData,
    bool IsEnabled,
    VBanQuality Quality)
    : ICommand<OperationResult<Endpoint>>;

public sealed class CreateStreamEndpointCommandHandler(
    IEndpointCatalogService endpointCatalogService,
    IEndpointAddressValidator endpointAddressValidator,
    IEndpointRepository endpointRepository)
    : ICommandHandler<CreateStreamEndpointCommand, OperationResult<Endpoint>>
{
    public async ValueTask<OperationResult<Endpoint>> Handle(
        CreateStreamEndpointCommand command,
        CancellationToken cancellationToken)
    {
        string? error = StreamEndpointValidation.ValidateName(command.Name);
        if (error != null)
        {
            return OperationResult<Endpoint>.Fail(error);
        }

        error = StreamEndpointValidation.ValidateHostname(command.Hostname);
        if (error != null)
        {
            return OperationResult<Endpoint>.Fail(error);
        }

        error = StreamEndpointValidation.ValidatePort(command.Port);
        if (error != null)
        {
            return OperationResult<Endpoint>.Fail(error);
        }

        var endpoints = endpointCatalogService.GetEndpoints(command.StreamType);

        if (StreamEndpointValidation.HasIdentityCollision(
            endpoints,
            command.StreamType,
            excludingEndpointId: null,
            streamName: command.Name,
            hostname: command.Hostname,
            port: command.Port))
        {
            return OperationResult<Endpoint>.Fail("A stream with the same identity already exists.");
        }

        Endpoint endpoint;
        try
        {
            endpoint = new Endpoint(
                command.StreamType,
                command.Name,
                command.Hostname,
                command.Port,
                command.IsDenoiseEnabled,
                command.AllowMobileData)
            {
                IsEnabled = command.IsEnabled,
                Quality = command.Quality
            };
        }
        catch (Exception ex)
        {
            return OperationResult<Endpoint>.Fail($"Failed to create endpoint: {ex.Message}");
        }

        if (!endpointAddressValidator.CanResolveHost(endpoint.Hostname))
        {
            return OperationResult<Endpoint>.Fail("Could not resolve hostname or create UDP endpoint.");
        }

        try
        {
            await endpointRepository.CreateAsync(endpoint);
        }
        catch (Exception ex)
        {
            return OperationResult<Endpoint>.Fail($"Failed to persist stream endpoint: {ex.Message}");
        }

        endpointCatalogService.AddEndpoint(endpoint);
        return OperationResult<Endpoint>.Success(endpoint);
    }
}
