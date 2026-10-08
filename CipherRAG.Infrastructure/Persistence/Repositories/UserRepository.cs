using CipherRAG.Application.Interfaces;
using CipherRAG.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CipherRAG.Application.Responses;

namespace CipherRAG.Infrastructure.Persistence.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context , IMemoryCache cache) : base(context , cache)
    {
        
    } 

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetUserWithRolesAsync(Guid userId)
    {
        return await _dbSet
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<IEnumerable<User>> GetAllWithRolesAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(u => u.Role)
            .ToListAsync();
    }

    public async Task<PagedResultResponse<User>> GetAllWithRolesAsyncPaged(int pageNumber, int pageSize)
    {
        var total = await _dbSet.CountAsync();
        var items = await _dbSet
            .AsNoTracking()
            .Include(u => u.Role)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResultResponse<User>(items, total, totalPages, pageNumber, pageSize);
    }
}
