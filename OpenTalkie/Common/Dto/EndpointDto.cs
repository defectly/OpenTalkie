using AutoMapper;
using OpenTalkie.Common.Enums;

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
}

public class EndpointDtoMappingProfile : Profile
{
    public EndpointDtoMappingProfile()
    {
        CreateMap<EndpointDto, Endpoint>()
            .ForMember(dest => dest.FrameCount, src => src.Ignore())
        .ReverseMap();
    }
}