using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.Rules;
using OpenTalkie.Domain.VBAN;
using System.Text.Json;

namespace OpenTalkie.Infrastructure.Repositories;

public class EndpointRepository : IEndpointRepository
{
    private readonly string endpointDirectory = Path.Combine(FileSystem.Current.AppDataDirectory, "Endpoints");
    private readonly List<PersistedEndpoint> persistedEndpoints = [];
    private readonly ILogger<EndpointRepository> logger;

    public EndpointRepository(ILogger<EndpointRepository> logger)
    {
        this.logger = logger;
        Load();
    }

    public List<Endpoint> List()
    {
        return [.. persistedEndpoints.Select(ToEndpoint)];
    }

    public Endpoint Get(Guid id)
    {
        var endpoint = persistedEndpoints.FirstOrDefault(e => e.Id == id);
        return endpoint == null
            ? throw new Exception($"Endpoint with id {id} not found")
            : ToEndpoint(endpoint);
    }

    public async Task<Guid> CreateAsync(Endpoint endpoint)
    {
        var persisted = ToPersisted(endpoint);
        EnsureNoIdentityCollision(persisted, excludingEndpointId: null);
        persistedEndpoints.Add(persisted);
        await SaveAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Persisted endpoint {EndpointId} created. Total endpoints: {EndpointCount}.", endpoint.Id, persistedEndpoints.Count);

        return endpoint.Id;
    }

    public async Task RemoveAsync(Guid id)
    {
        var endpoint = persistedEndpoints.FirstOrDefault(e => e.Id == id)
            ?? throw new Exception($"Endpoint with id {id} not found");

        persistedEndpoints.Remove(endpoint);

        var filePath = Path.Combine(endpointDirectory, $"{endpoint.Id}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Deleted endpoint file {FilePath}.", filePath);
        }

        await SaveAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Persisted endpoint {EndpointId} removed. Total endpoints: {EndpointCount}.", id, persistedEndpoints.Count);
    }

    public async Task UpdateAsync(Endpoint endpoint)
    {
        var existing = persistedEndpoints.FirstOrDefault(e => e.Id == endpoint.Id)
            ?? throw new Exception($"Endpoint with id {endpoint.Id} not found");

        var candidate = ToPersisted(endpoint);
        EnsureNoIdentityCollision(candidate, excludingEndpointId: endpoint.Id);

        existing.Type = candidate.Type;
        existing.Name = candidate.Name;
        existing.Hostname = candidate.Hostname;
        existing.Port = candidate.Port;
        existing.IsEnabled = candidate.IsEnabled;
        existing.IsDenoiseEnabled = candidate.IsDenoiseEnabled;
        existing.AllowMobileData = candidate.AllowMobileData;
        existing.Quality = candidate.Quality;
        existing.Volume = candidate.Volume;

        await SaveAsync();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Persisted endpoint {EndpointId} updated.", endpoint.Id);
    }

    private void EnsureNoIdentityCollision(PersistedEndpoint candidate, Guid? excludingEndpointId)
    {
        for (var i = 0; i < persistedEndpoints.Count; i++)
        {
            var existing = persistedEndpoints[i];

            if (excludingEndpointId.HasValue && existing.Id == excludingEndpointId.Value)
                continue;

            if (EndpointIdentityRules.Collides(
                candidate.Type,
                candidate.Name,
                candidate.Hostname,
                candidate.Port,
                existing.Type,
                existing.Name,
                existing.Hostname,
                existing.Port))
            {
                throw new InvalidOperationException("A stream with the same identity already exists.");
            }
        }
    }

    private async Task SaveAsync()
    {
        if (!Directory.Exists(endpointDirectory))
        {
            Directory.CreateDirectory(endpointDirectory);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Created endpoint storage directory {Directory}.", endpointDirectory);
        }

        for (var i = 0; i < persistedEndpoints.Count; i++)
        {
            var persisted = persistedEndpoints[i];
            var json = JsonSerializer.Serialize(persisted, EndpointJsonSerializerContext.Default.PersistedEndpoint);
            var filePath = Path.Combine(endpointDirectory, $"{persisted.Id}.json");
            await File.WriteAllTextAsync(filePath, json);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Saved endpoint {EndpointId} to {FilePath}.", persisted.Id, filePath);
        }
    }

    private void Load()
    {
        if (!Directory.Exists(endpointDirectory))
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Endpoint storage directory {Directory} does not exist yet.", endpointDirectory);

            return;
        }

        var filePaths = Directory.EnumerateFiles(endpointDirectory).ToList();
        for (var i = 0; i < filePaths.Count; i++)
        {
            var file = File.ReadAllText(filePaths[i]);
            var persisted = JsonSerializer.Deserialize(file, EndpointJsonSerializerContext.Default.PersistedEndpoint);

            if (persisted != null)
                persistedEndpoints.Add(persisted);
            else
                logger.LogWarning("Endpoint file {FilePath} did not contain a valid endpoint.", filePaths[i]);
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Loaded {EndpointCount} persisted endpoint(s).", persistedEndpoints.Count);
    }

    private static PersistedEndpoint ToPersisted(Endpoint endpoint)
    {
        return new PersistedEndpoint
        {
            Id = endpoint.Id,
            Type = endpoint.Type,
            Name = endpoint.Name,
            Hostname = endpoint.Hostname,
            Port = endpoint.Port,
            IsEnabled = endpoint.IsEnabled,
            IsDenoiseEnabled = endpoint.IsDenoiseEnabled,
            AllowMobileData = endpoint.AllowMobileData,
            Volume = endpoint.Volume,
            Quality = endpoint.Quality
        };
    }

    private static Endpoint ToEndpoint(PersistedEndpoint persisted)
    {
        return new Endpoint
        {
            Id = persisted.Id,
            Type = persisted.Type,
            Name = persisted.Name,
            Hostname = persisted.Hostname,
            Port = persisted.Port,
            IsEnabled = persisted.IsEnabled,
            IsDenoiseEnabled = persisted.IsDenoiseEnabled,
            AllowMobileData = persisted.AllowMobileData,
            Volume = persisted.Volume,
            Quality = persisted.Quality
        };
    }

}

internal sealed class PersistedEndpoint
{
    public Guid Id { get; set; }
    public EndpointType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDenoiseEnabled { get; set; }
    public bool AllowMobileData { get; set; }
    public float Volume { get; set; } = 1f;
    public VBanQuality Quality { get; set; } = VBanQuality.VBAN_QUALITY_FAST;
}
