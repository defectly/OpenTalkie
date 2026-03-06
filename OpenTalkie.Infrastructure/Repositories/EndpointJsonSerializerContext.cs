using System.Text.Json.Serialization;

namespace OpenTalkie.Infrastructure.Repositories;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(PersistedEndpoint))]
internal partial class EndpointJsonSerializerContext : JsonSerializerContext
{
}
