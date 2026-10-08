using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.Responses;

namespace CipherRAG.Infrastructure.Persistence.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    protected readonly DbSet<T> _dbSet;
    private readonly string _cacheKey;

    // Track paged cache keys so they can be invalidated on writes
    private readonly HashSet<string> _pagedCacheKeys = [];

    public GenericRepository(AppDbContext context, IMemoryCache cache)
    {
        _context  = context;
        _cache    = cache;
        _dbSet    = context.Set<T>();
        _cacheKey = $"All_{typeof(T).Name}";
    }

    // Constructor overload for derived repositories that don't use cache directly
    public GenericRepository(AppDbContext context)
    {
        _context  = context;
        _cache    = new MemoryCache(new MemoryCacheOptions());
        _dbSet    = context.Set<T>();
        _cacheKey = $"All_{typeof(T).Name}";
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _cache.GetOrCreateAsync(_cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return (IEnumerable<T>)await _dbSet.AsNoTracking().ToListAsync();
        }) ?? [];
    }

    public async Task<PagedResultResponse<T>> GetAllAsyncPaged(int pageNumber, int pageSize)
    {
        var pagedKey = $"{_cacheKey}_Page_{pageNumber}_Size_{pageSize}";
        _pagedCacheKeys.Add(pagedKey);

        var result = await _cache.GetOrCreateAsync(pagedKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            var total = await _dbSet.CountAsync();
            var items = await _dbSet
                .AsNoTracking()
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            int totalPages = (int)Math.Ceiling(total / (double)pageSize);
            return new PagedResultResponse<T>(items, total, totalPages, pageNumber, pageSize);
        });

        return result ?? new PagedResultResponse<T>([], 0, 0, pageNumber, pageSize);
    }

    public async Task<T?> GetByIdAsync(Guid id)
        => await _dbSet.FindAsync(id);

    public async Task<int> GetCountAsync()
        => await _dbSet.CountAsync();

    public async Task AddAsync(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _dbSet.AddAsync(entity);
        InvalidateAllCaches();
    }

    public void Update(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbSet.Update(entity);
        InvalidateAllCaches();
    }

    public void Delete(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _dbSet.Remove(entity);
        InvalidateAllCaches();
    }

    private void InvalidateAllCaches()
    {
        _cache.Remove(_cacheKey);
        // Invalidate all tracked paged cache keys
        foreach (var key in _pagedCacheKeys)
            _cache.Remove(key);
        _pagedCacheKeys.Clear();
    }
}