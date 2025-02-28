using System.Text.Json;

namespace OpenTalkie.Repositories;

public class EndpointRepository
{
    private string _endpointDirectory = Path.Combine(FileSystem.Current.AppDataDirectory, "Endpoints");
    public List<Endpoint> Endpoints { get; private set; }

    public EndpointRepository()
    {
        LoadPreferences();
    }

    public void Add(Endpoint endpoint)
    {
        Endpoints.Add(endpoint);

        SavePreferencesAsync()
            .ConfigureAwait(false);
    }

    public void Remove(Endpoint endpoint)
    {
        Endpoints.Remove(endpoint);

        SavePreferencesAsync()
            .ConfigureAwait(false);
    }

    private void LoadPreferences()
    {
        Endpoints ??= [];

        if (!Directory.Exists(_endpointDirectory))
            return;

        foreach (var filePath in Directory.EnumerateFiles(_endpointDirectory))
        {
            var file = File.OpenRead(filePath);
            var endpoint = JsonSerializer.Deserialize<Endpoint>(file);

            if (endpoint == null)
                continue;

            if (Endpoints.FirstOrDefault(e => e.Id == endpoint.Id) == null)
                continue;

            Endpoints.Add(endpoint);
        }
    }

    private async Task SavePreferencesAsync()
    {
        if (!Directory.Exists(_endpointDirectory))
            Directory.CreateDirectory(_endpointDirectory);

        await Parallel.ForEachAsync(Endpoints, async (endpoint, ct) =>
        {
            var json = JsonSerializer.Serialize(endpoint);
            string filePath = $"{Path.Combine(_endpointDirectory, endpoint.Id.ToString())}.json";

            await File.WriteAllTextAsync($"{filePath}", json, ct);
        });
    }
}
