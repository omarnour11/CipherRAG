using CipherRAG.Application.Responses;

namespace CipherRAG.Application.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(Guid Id);
    Task<int> GetCountAsync();
        Task<IEnumerable<T>> GetAllAsync();
        Task<PagedResultResponse<T>> GetAllAsyncPaged(int pageNumber, int pageSize);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
    }
}