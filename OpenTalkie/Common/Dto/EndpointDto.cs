﻿using AutoMapper;
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
            .ForMember(dest => dest.Id, src => src.MapFrom(endpoint => endpoint.Id))
            .ForMember(dest => dest.Type, src => src.MapFrom(endpoint => endpoint.Type))
            .ForMember(dest => dest.Name, src => src.MapFrom(endpoint => endpoint.Name))
            .ForMember(dest => dest.Hostname, src => src.MapFrom(endpoint => endpoint.Hostname))
            .ForMember(dest => dest.Port, src => src.MapFrom(endpoint => endpoint.Port))
            .ForMember(dest => dest.IsEnabled, src => src.MapFrom(endpoint => endpoint.IsEnabled))
            .ForMember(dest => dest.IsDenoiseEnabled, src => src.MapFrom(endpoint => endpoint.IsDenoiseEnabled))
        .ReverseMap();
    }
}