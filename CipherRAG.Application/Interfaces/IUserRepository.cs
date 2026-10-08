using CipherRAG.Domain.Entities;
using CipherRAG.Application.Responses;

namespace CipherRAG.Application.Interfaces
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);

        Task<User?> GetUserWithRolesAsync(Guid userId);

        Task<IEnumerable<User>> GetAllWithRolesAsync();

        Task<PagedResultResponse<User>> GetAllWithRolesAsyncPaged(int pageNumber, int pageSize);
    }
}