using Mediator;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.VBAN;

namespace OpenTalkie.Application.Streams.Commands;

public readonly record struct UpdateStreamEndpointCommand(
    EndpointType StreamType,
    Guid EndpointId,
    string? Name = null,
    string? Hostname = null,
    int? Port = null,
    VBanQuality? Quality = null,
    bool? IsDenoiseEnabled = null,
    bool? AllowMobileData = null,
    bool? IsEnabled = null,
    float? Volume = null)
    : ICommand<OperationResult>;

public sealed class UpdateStreamEndpointCommandHandler(
    IEndpointCatalogService endpointCatalogService,
    IEndpointAddressValidator endpointAddressValidator,
    IEndpointRepository endpointRepository,
    ILogger<UpdateStreamEndpointCommandHandler> logger)
    : ICommandHandler<UpdateStreamEndpointCommand, OperationResult>
{
    public async ValueTask<OperationResult> Handle(
        UpdateStreamEndpointCommand command,
        CancellationToken cancellationToken)
    {
        var endpoints = endpointCatalogService.GetEndpoints(command.StreamType);

        var endpoint = endpoints.FirstOrDefault(e => e.Id == command.EndpointId);
        if (endpoint == null)
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Update {StreamType} endpoint {EndpointId} rejected because it was not found.", command.StreamType, command.EndpointId);

            return OperationResult.Fail("Stream endpoint was not found.");
        }

        bool destinationChanged = command.Hostname != null || command.Port.HasValue;
        bool identityChanged = command.Name != null || destinationChanged;
        string proposedName = command.Name ?? endpoint.Name;
        string proposedHostname = command.Hostname ?? endpoint.Hostname;
        int proposedPort = command.Port ?? endpoint.Port;

        if (command.Name != null)
        {
            string? nameError = StreamEndpointValidation.ValidateName(command.Name);
            if (nameError != null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Update {StreamType} endpoint {EndpointId} rejected: {Error}.", command.StreamType, command.EndpointId, nameError);

                return OperationResult.Fail(nameError);
            }
        }

        if (command.Volume is float volumeToValidate)
        {
            string? volumeError = StreamEndpointValidation.ValidateVolume(volumeToValidate);
            if (volumeError != null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Update {StreamType} endpoint {EndpointId} rejected: {Error}.", command.StreamType, command.EndpointId, volumeError);

                return OperationResult.Fail(volumeError);
            }
        }

        if (destinationChanged)
        {
            string? hostError = StreamEndpointValidation.ValidateHostname(proposedHostname);
            if (hostError != null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Update {StreamType} endpoint {EndpointId} rejected: {Error}.", command.StreamType, command.EndpointId, hostError);

                return OperationResult.Fail(hostError);
            }

            string? portError = StreamEndpointValidation.ValidatePort(proposedPort);
            if (portError != null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Update {StreamType} endpoint {EndpointId} rejected: {Error}.", command.StreamType, command.EndpointId, portError);

                return OperationResult.Fail(portError);
            }

            if (!endpointAddressValidator.CanResolveHost(proposedHostname))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("Update {StreamType} endpoint {EndpointId} rejected because host {Host} could not be resolved.", command.StreamType, command.EndpointId, proposedHostname);

                return OperationResult.Fail("Could not resolve hostname or create UDP endpoint.");
            }
        }

        if (identityChanged && StreamEndpointValidation.HasIdentityCollision(
                endpoints,
                command.StreamType,
                excludingEndpointId: endpoint.Id,
                streamName: proposedName,
                hostname: proposedHostname,
                port: proposedPort))
        {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("Update {StreamType} endpoint {EndpointId} rejected because identity already exists for {Host}:{Port}.", command.StreamType, command.EndpointId, proposedHostname, proposedPort);

            return OperationResult.Fail("A stream with the same identity already exists.");
        }

        var updatedEndpoint = new Endpoint
        {
            Id = endpoint.Id,
            Type = endpoint.Type,
            Name = proposedName,
            Hostname = proposedHostname,
            Port = proposedPort,
            Quality = command.Quality ?? endpoint.Quality,
            IsDenoiseEnabled = command.IsDenoiseEnabled ?? endpoint.IsDenoiseEnabled,
            AllowMobileData = command.AllowMobileData ?? endpoint.AllowMobileData,
            IsEnabled = command.IsEnabled ?? endpoint.IsEnabled,
            Volume = command.Volume ?? endpoint.Volume
        };

        try
        {
            await endpointRepository.UpdateAsync(updatedEndpoint);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(ex, "Failed to persist update for {StreamType} endpoint {EndpointId}.", command.StreamType, command.EndpointId);

            return OperationResult.Fail($"Failed to persist stream endpoint update: {ex.Message}");
        }

        if (!endpointCatalogService.UpdateEndpoint(updatedEndpoint))
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError("Updated {StreamType} endpoint {EndpointId} could not be synchronized in memory.", command.StreamType, command.EndpointId);

            return OperationResult.Fail("Updated stream endpoint could not be synchronized in memory.");
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Updated {StreamType} endpoint {EndpointId}.", command.StreamType, command.EndpointId);

        return OperationResult.Success();
    }
}
