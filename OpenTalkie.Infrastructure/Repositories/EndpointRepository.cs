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

    public EndpointRepository()
    {
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
        }

        await SaveAsync();
    }

    public async Task UpdateAsync(Endpoint endpoint)
    {
        var existing = persistedEndpoints.FirstOrDefault(e => e.Id == endpoint.Id);
        if (existing == null)
        {
            throw new Exception($"Endpoint with id {endpoint.Id} not found");
        }

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
    }

    private void EnsureNoIdentityCollision(PersistedEndpoint candidate, Guid? excludingEndpointId)
    {
        for (var i = 0; i < persistedEndpoints.Count; i++)
        {
            var existing = persistedEndpoints[i];
            if (excludingEndpointId.HasValue && existing.Id == excludingEndpointId.Value)
            {
                continue;
            }

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
        }

        for (var i = 0; i < persistedEndpoints.Count; i++)
        {
            var persisted = persistedEndpoints[i];
            var json = JsonSerializer.Serialize(persisted, EndpointJsonSerializerContext.Default.PersistedEndpoint);
            var filePath = Path.Combine(endpointDirectory, $"{persisted.Id}.json");
            await File.WriteAllTextAsync(filePath, json);
        }
    }

    private void Load()
    {
        if (!Directory.Exists(endpointDirectory))
        {
            return;
        }

        var filePaths = Directory.EnumerateFiles(endpointDirectory).ToList();
        for (var i = 0; i < filePaths.Count; i++)
        {
            var file = File.ReadAllText(filePaths[i]);
            var persisted = JsonSerializer.Deserialize(file, EndpointJsonSerializerContext.Default.PersistedEndpoint);
            if (persisted != null)
            {
                persistedEndpoints.Add(persisted);
            }
        }
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
