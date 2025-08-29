using OpenTalkie.Common.Dto;
using OpenTalkie.Common.Repositories.Interfaces;
using System.Text.Json;

namespace OpenTalkie.Common.Repositories;

public class EndpointRepository : IEndpointRepository
{
    private readonly string _endpointDirectory = Path.Combine(FileSystem.Current.AppDataDirectory, "Endpoints");
    public List<EndpointDto> Endpoints { get; private set; } = [];

    public EndpointRepository()
    {
        Load();
    }

    public List<EndpointDto> List()
    {
        var endpointsDto = new List<EndpointDto>();
        endpointsDto.AddRange(Endpoints);

        return endpointsDto;
    }

    public EndpointDto Get(Guid id)
    {
        var endpoint = Endpoints.FirstOrDefault(e => e.Id == id);

        return endpoint ?? throw new Exception($"Endpoint with id {id} not found");
    }

    public async Task<Guid> CreateAsync(EndpointDto endpointDto)
    {
        var endpoint = new EndpointDto
        {
            Id = endpointDto.Id,
            Type = endpointDto.Type,
            Name = endpointDto.Name,
            Hostname = endpointDto.Hostname,
            Port = endpointDto.Port,
            IsEnabled = endpointDto.IsEnabled,
            IsDenoiseEnabled = endpointDto.IsDenoiseEnabled
        };

        Endpoints.Add(endpoint);

        await SaveAsync();

        return endpointDto.Id;
    }

    public async Task RemoveAsync(Guid id)
    {
        var endpoint = Endpoints.FirstOrDefault(e => e.Id == id) ?? throw new Exception($"Endpoint with id {id} not found");

        Endpoints.Remove(endpoint);

        string filePath = $"{Path.Combine(_endpointDirectory, endpoint.Id.ToString())}.json";

        if (File.Exists(filePath))
            File.Delete(filePath);

        await SaveAsync();
    }

    public async Task UpdateAsync(EndpointDto endpointDto)
    {
        var endpoint = Endpoints.FirstOrDefault(e => e.Id == endpointDto.Id);

        if (endpoint == null)
            throw new Exception($"Endpoint with id {endpointDto.Id} not found");

        endpoint.Type = endpointDto.Type;
        endpoint.Name = endpointDto.Name;
        endpoint.Hostname = endpointDto.Hostname;
        endpoint.Port = endpointDto.Port;
        endpoint.IsEnabled = endpointDto.IsEnabled;
        endpoint.IsDenoiseEnabled = endpointDto.IsDenoiseEnabled;

        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        if (!Directory.Exists(_endpointDirectory))
            Directory.CreateDirectory(_endpointDirectory);

        for (int i = 0; i < Endpoints.Count; i++)
        {
            var json = JsonSerializer.Serialize(Endpoints[i]);
            string filePath = $"{Path.Combine(_endpointDirectory, Endpoints[i].Id.ToString())}.json";

            await File.WriteAllTextAsync($"{filePath}", json);
        }
    }

    private void Load()
    {
        if (!Directory.Exists(_endpointDirectory))
            return;

        var filePaths = Directory.EnumerateFiles(_endpointDirectory).ToList();

        for (int i = 0; i < filePaths.Count; i++)
        {
            var filePath = filePaths[i];
            var file = File.ReadAllText(filePath);
            var endpointDto = JsonSerializer.Deserialize<EndpointDto>(file);

            if (endpointDto == null)
                continue;

            Endpoints.Add(endpointDto);
        }
    }
}
