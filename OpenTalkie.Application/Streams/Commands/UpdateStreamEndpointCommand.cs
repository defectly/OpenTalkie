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
    float? Volume = null,
    float? MaximumVolume = null)
    : ICommand<OperationResult>;

public sealed class UpdateStreamEndpointCommandHandler(
    IEndpointCatalogService endpointCatalogService,
    IEndpointAddressValidator endpointAddressValidator,
    IEndpointRepository endpointRepository)
    : ICommandHandler<UpdateStreamEndpointCommand, OperationResult>
{
    public async ValueTask<OperationResult> Handle(
        UpdateStreamEndpointCommand command,
        CancellationToken cancellationToken)
    {
        var endpoints = endpointCatalogService.GetEndpoints(command.StreamType);

        var endpoint = endpoints.FirstOrDefault(e => e.Id == command.EndpointId);

        if (endpoint == null)
            return OperationResult.Fail("Stream endpoint was not found.");

        bool destinationChanged = command.Hostname != null || command.Port.HasValue;
        bool identityChanged = command.Name != null || destinationChanged;
        string proposedName = command.Name ?? endpoint.Name;
        string proposedHostname = command.Hostname ?? endpoint.Hostname;
        int proposedPort = command.Port ?? endpoint.Port;

        if (command.Name != null)
        {
            string? nameError = StreamEndpointValidation.ValidateName(command.Name);

            if (nameError != null)
                return OperationResult.Fail(nameError);
        }

        float maximum = command.MaximumVolume ?? endpoint.MaximumVolume;
        if (!float.IsFinite(maximum) || maximum < 1f)
            return OperationResult.Fail("Maximum volume multiplier must be a finite number of 1 or greater.");

        if (command.Volume is float volumeToValidate)
        {
            string? volumeError = StreamEndpointValidation.ValidateVolume(volumeToValidate, maximum);

            if (volumeError != null)
                return OperationResult.Fail(volumeError);
        }

        if (destinationChanged)
        {
            string? hostError = StreamEndpointValidation.ValidateHostname(proposedHostname);

            if (hostError != null)
                return OperationResult.Fail(hostError);

            string? portError = StreamEndpointValidation.ValidatePort(proposedPort);

            if (portError != null)
                return OperationResult.Fail(portError);

            if (!endpointAddressValidator.CanResolveHost(proposedHostname))
                return OperationResult.Fail("Could not resolve hostname or create UDP endpoint.");
        }

        if (identityChanged && StreamEndpointValidation.HasIdentityCollision(
                endpoints,
                command.StreamType,
                excludingEndpointId: endpoint.Id,
                streamName: proposedName,
                hostname: proposedHostname,
                port: proposedPort))
        {
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
            Volume = Math.Min(command.Volume ?? endpoint.Volume, maximum),
            MaximumVolume = maximum
        };

        try
        {
            await endpointRepository.UpdateAsync(updatedEndpoint);
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Failed to persist stream endpoint update: {ex.Message}");
        }

        if (!endpointCatalogService.UpdateEndpoint(updatedEndpoint))
        {
            return OperationResult.Fail("Updated stream endpoint could not be synchronized in memory.");
        }

        return OperationResult.Success();
    }
}
