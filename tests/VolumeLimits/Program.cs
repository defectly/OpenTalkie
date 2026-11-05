using Mediator;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenTalkie.Application.Abstractions.Repositories;
using OpenTalkie.Application.Abstractions.Services;
using OpenTalkie.Application.DependencyInjection;
using OpenTalkie.Application.Settings;
using OpenTalkie.Application.Settings.Commands;
using OpenTalkie.Application.Streams.Commands;
using OpenTalkie.Domain.Enums;
using OpenTalkie.Domain.Models;
using OpenTalkie.Infrastructure.Services;

var services = new ServiceCollection().AddApplicationLayer();
var limits = new VolumeLimits();
var globals = new GlobalVolumes();
var endpoints = new Endpoints();
services.AddSingleton<IVolumeLimitRepository>(limits);
services.AddSingleton<IMicrophoneRepository>(globals);
services.AddSingleton<IPlaybackRepository>(globals);
services.AddSingleton<IReceiverRepository>(globals);
services.AddSingleton<IEndpointRepository>(endpoints);
services.AddSingleton<IEndpointCatalogService, EndpointCatalogService>();
services.AddSingleton<IEndpointAddressValidator, AddressValidator>();
// Mediator initializes every handler; these unrelated dependencies must stay unused.
foreach (Type type in new[] { typeof(IMicrophoneBroadcastService), typeof(IMicrophonePermissionService),
    typeof(IPlaybackBroadcastService), typeof(IReceiverService), typeof(IAudioManagerSettingsRepository),
    typeof(IDenoiseAvailabilityService), typeof(IPlatformCapabilitiesService) })
    services.AddSingleton(type, DispatchProxy.Create(type, typeof(UnusedService)));
using var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
var catalog = provider.GetRequiredService<IEndpointCatalogService>();

foreach (var type in Enum.GetValues<EndpointType>())
{
    var first = new Endpoint { Id = Guid.NewGuid(), Type = type, Name = "first", Hostname = "localhost", Port = 6980 };
    var second = new Endpoint { Id = Guid.NewGuid(), Type = type, Name = "second", Hostname = "localhost", Port = 6980 };
    await endpoints.CreateAsync(first); await endpoints.CreateAsync(second);
    catalog.AddEndpoint(first); catalog.AddEndpoint(second);
    Check(!(await SetGlobal(type, 1.1f)).IsSuccess, "Default global maximum is 1");
    Check(!(await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, Volume: 1.1f))).IsSuccess,
        "Default stream maximum is 1");
    foreach (float invalid in new[] { 0f, 0.9f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        Check(!(await mediator.Send(new SetVolumeMaximumCommand(type, invalid))).IsSuccess, "Invalid global maximum rejected");
        Check(!(await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, MaximumVolume: invalid))).IsSuccess,
            "Invalid stream maximum rejected");
    }
    var otherGlobals = Enum.GetValues<EndpointType>().Where(t => t != type).ToDictionary(t => t, limits.GetMaximum);
    Check((await mediator.Send(new SetVolumeMaximumCommand(type, 5f))).IsSuccess, "Raise global maximum");
    Check((await SetGlobal(type, 4f)).IsSuccess, "Global amplification");
    Check((await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, MaximumVolume: 8f, Volume: 7f))).IsSuccess,
        "First stream gets independent maximum");
    Check((await mediator.Send(new UpdateStreamEndpointCommand(type, second.Id, MaximumVolume: 3f, Volume: 2.5f))).IsSuccess,
        "Second stream gets independent maximum");
    Check((await mediator.Send(new SetVolumeMaximumCommand(type, 2f))).IsSuccess && globals.Values[type] == 2f,
        "Global decrease clamps only global volume");
    Check(catalog.GetEndpoint(type, first.Id)!.Volume == 7f && catalog.GetEndpoint(type, second.Id)!.Volume == 2.5f,
        "Global maximum leaves both streams unchanged");
    Check(otherGlobals.All(pair => limits.GetMaximum(pair.Key) == pair.Value), "Other global maxima unchanged");
    Check((await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, MaximumVolume: 1.5f))).IsSuccess,
        "Lower first stream maximum");
    Check(endpoints.Get(first.Id).MaximumVolume == 1.5f && endpoints.Get(first.Id).Volume == 1.5f,
        "Maximum and clamped stream volume persisted together");
    Check(catalog.GetEndpoint(type, second.Id)!.Volume == 2.5f && globals.Values[type] == 2f,
        "Stream maximum leaves other stream and global volume unchanged");
    endpoints.FailUpdates = true;
    Check(!(await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, MaximumVolume: 1f))).IsSuccess,
        "Persistence failure returned");
    Check(catalog.GetEndpoint(type, first.Id)!.MaximumVolume == 1.5f, "Failed persistence leaves runtime maximum unchanged");
    endpoints.FailUpdates = false;
    foreach (float invalid in new[] { -1f, 1.6f, float.NaN, float.PositiveInfinity })
        Check(!(await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, Volume: invalid))).IsSuccess,
            "Stream gain is checked against its own maximum");
    foreach (float maximum in new[] { 4f, 8f, 1000f, float.MaxValue })
    {
        Check((await mediator.Send(new SetVolumeMaximumCommand(type, maximum))).IsSuccess, "Large global maximum");
        Check((await SetGlobal(type, maximum)).IsSuccess, "Large global gain");
        Check((await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, MaximumVolume: maximum, Volume: maximum))).IsSuccess,
            "Large stream maximum and gain");
    }
    Check((await SetGlobal(type, 0f)).IsSuccess && (await SetGlobal(type, 1f)).IsSuccess, "Global mute and reset");
    Check((await mediator.Send(new UpdateStreamEndpointCommand(type, first.Id, Volume: 1f))).IsSuccess,
        "Stream reset keeps its maximum");
    Check(catalog.GetEndpoint(type, first.Id)!.MaximumVolume == float.MaxValue, "Reset preserves stream maximum");
}
PcmVolumeChecks.Run();
AudioPipelineChecks.Run();
Console.WriteLine("Volume checks passed: independent global and per-stream maxima, isolation, clamping, persistence failure and large gains.");

ValueTask<OperationResult> SetGlobal(EndpointType type, float gain) => type switch
{
    EndpointType.Microphone => mediator.Send(new SetMicrophoneVolumeCommand(gain)),
    EndpointType.Playback => mediator.Send(new SetPlaybackVolumeCommand(gain)),
    EndpointType.Receiver => mediator.Send(new SetReceiverVolumeCommand(gain)),
    _ => throw new ArgumentOutOfRangeException(nameof(type))
};
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class VolumeLimits : IVolumeLimitRepository
{
    private readonly Dictionary<EndpointType, float> values = [];
    public float GetMaximum(EndpointType type) => values.GetValueOrDefault(type, 1f);
    public void SetMaximum(EndpointType type, float value) => values[type] = value;
}

sealed class GlobalVolumes : IMicrophoneRepository, IPlaybackRepository, IReceiverRepository
{
    public Dictionary<EndpointType, float> Values { get; } = Enum.GetValues<EndpointType>().ToDictionary(t => t, _ => 1f);
    public event Action<float>? VolumeChanged;
    public event Action<string>? PreferredAudioInputDeviceChanged { add { } remove { } }
    public event Action<string>? PreferredAudioOutputDeviceChanged { add { } remove { } }
    MicrophoneSettingsState IMicrophoneRepository.GetSettings() => new(default, default, default, default, 960, Values[EndpointType.Microphone], default, false);
    PlaybackSettingsState IPlaybackRepository.GetSettings() => new(default, default, default, 1920, Values[EndpointType.Playback]);
    ReceiverSettingsState IReceiverRepository.GetSettings() => new(Values[EndpointType.Receiver], default);
    void IMicrophoneRepository.SetSelectedVolume(float value) => SetVolume(EndpointType.Microphone, value);
    void IPlaybackRepository.SetSelectedVolume(float value) => SetVolume(EndpointType.Playback, value);
    void IReceiverRepository.SetSelectedVolume(float value) => SetVolume(EndpointType.Receiver, value);
    private void SetVolume(EndpointType type, float value) { Values[type] = value; VolumeChanged?.Invoke(value); }
    public IReadOnlyList<SettingOptionItem> GetOptions(MicrophoneSettingOption option) => [];
    public IReadOnlyList<SettingOptionItem> GetOptions(PlaybackSettingOption option) => [];
    public IReadOnlyList<SettingOptionItem> GetAudioOutputOptions() => [];
    public void SetBufferSize(int value) { }
    public void SetOption(MicrophoneSettingOption option, string value) { }
    public void SetOption(PlaybackSettingOption option, string value) { }
    public void SetPacingEnabled(bool enabled) { }
    public void SetPreferredDevice(string value) { }
}

sealed class Endpoints : IEndpointRepository
{
    private readonly Dictionary<Guid, Endpoint> values = [];
    public bool FailUpdates { get; set; }
    public Task<Guid> CreateAsync(Endpoint endpoint) { values.Add(endpoint.Id, endpoint); return Task.FromResult(endpoint.Id); }
    public Endpoint Get(Guid id) => values[id];
    public List<Endpoint> List() => [.. values.Values];
    public Task RemoveAsync(Guid id) { values.Remove(id); return Task.CompletedTask; }
    public Task UpdateAsync(Endpoint endpoint)
    {
        if (FailUpdates) throw new IOException("test write failure");
        values[endpoint.Id] = endpoint;
        return Task.CompletedTask;
    }
}

sealed class AddressValidator : IEndpointAddressValidator
{
    public bool CanResolveHost(string hostname) => true;
}

class UnusedService : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        throw new InvalidOperationException($"Unexpected dependency call: {method?.Name}");
}
