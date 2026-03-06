namespace OpenTalkie.Application.Abstractions.Repositories
{
    public interface IEndpointRepository
    {
        Task<Guid> CreateAsync(Endpoint endpoint);
        Endpoint Get(Guid id);
        List<Endpoint> List();
        Task RemoveAsync(Guid id);
        Task UpdateAsync(Endpoint endpoint);
    }
}
