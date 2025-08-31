using AutoMapper;
using OpenTalkie.Common.Enums;
using OpenTalkie.VBAN;

namespace OpenTalkie.Common.Dto;

public class EndpointDto
{
    public Guid Id { get; set; }
    public EndpointType Type { get; set; }
    public required string Name { get; set; }
    public required string Hostname { get; set; }
    public int Port { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDenoiseEnabled { get; set; }
    public float Volume { get; set; } = 1f;
    public VBanQuality Quality { get; set; } = VBanQuality.VBAN_QUALITY_FAST;
}

public class EndpointDtoMappingProfile : Profile
{
    public EndpointDtoMappingProfile()
    {
        CreateMap<EndpointDto, Endpoint>()
            .ForMember(dest => dest.Id, src => src.MapFrom(endpoint => endpoint.Id))
            .ForMember(dest => dest.Type, src => src.MapFrom(endpoint => endpoint.Type))
            .ForMember(dest => dest.Name, src => src.MapFrom(endpoint => endpoint.Name))
            .ForMember(dest => dest.Hostname, src => src.MapFrom(endpoint => endpoint.Hostname))
            .ForMember(dest => dest.Port, src => src.MapFrom(endpoint => endpoint.Port))
            .ForMember(dest => dest.IsEnabled, src => src.MapFrom(endpoint => endpoint.IsEnabled))
            .ForMember(dest => dest.IsDenoiseEnabled, src => src.MapFrom(endpoint => endpoint.IsDenoiseEnabled))
            .ForMember(dest => dest.Volume, src => src.MapFrom(endpoint => endpoint.Volume))
            .ForMember(dest => dest.Quality, src => src.MapFrom(endpoint => endpoint.Quality))
        .ReverseMap();
    }
}
