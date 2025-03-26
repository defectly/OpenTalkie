using OpenTalkie.Common.Dto;

namespace OpenTalkie.Common.Repositories.Interfaces
{
    public interface IEndpointRepository
    {
        Task<Guid> CreateAsync(EndpointDto endpointDto);
        EndpointDto Get(Guid id);
        List<EndpointDto> List();
        Task RemoveAsync(Guid id);
        Task UpdateAsync(EndpointDto endpointDto);
    }
}