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
    IEndpointRepository endpointRepository,
    ILogger<CreateStreamEndpointCommandHandler> logger)
    : ICommandHandler<CreateStreamEndpointCommand, OperationResult<Endpoint>>
{
    public async ValueTask<OperationResult<Endpoint>> Handle(
        CreateStreamEndpointCommand command,
        CancellationToken cancellationToken)
    {
        string? error = StreamEndpointValidation.ValidateName(command.Name);
        if (error != null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Create {StreamType} endpoint rejected: {Error}.", command.StreamType, error);

            return OperationResult<Endpoint>.Fail(error);
        }

        error = StreamEndpointValidation.ValidateHostname(command.Hostname);
        if (error != null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Create {StreamType} endpoint rejected: {Error}.", command.StreamType, error);

            return OperationResult<Endpoint>.Fail(error);
        }

        error = StreamEndpointValidation.ValidatePort(command.Port);
        if (error != null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Create {StreamType} endpoint rejected: {Error}.", command.StreamType, error);

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
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Create {StreamType} endpoint rejected because identity already exists for {Host}:{Port}.", command.StreamType, command.Hostname, command.Port);

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
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(ex, "Failed to create endpoint model for {StreamType}.", command.StreamType);

            return OperationResult<Endpoint>.Fail($"Failed to create endpoint: {ex.Message}");
        }

        if (!endpointAddressValidator.CanResolveHost(endpoint.Hostname))
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Create {StreamType} endpoint rejected because host {Host} could not be resolved.", command.StreamType, endpoint.Hostname);

            return OperationResult<Endpoint>.Fail("Could not resolve hostname or create UDP endpoint.");
        }

        try
        {
            await endpointRepository.CreateAsync(endpoint);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(ex, "Failed to persist new {StreamType} endpoint {EndpointId}.", command.StreamType, endpoint.Id);

            return OperationResult<Endpoint>.Fail($"Failed to persist stream endpoint: {ex.Message}");
        }

        endpointCatalogService.AddEndpoint(endpoint);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Created {StreamType} endpoint {EndpointId} for {Host}:{Port}.", endpoint.Type, endpoint.Id, endpoint.Hostname, endpoint.Port);

        return OperationResult<Endpoint>.Success(endpoint);
    }
}
