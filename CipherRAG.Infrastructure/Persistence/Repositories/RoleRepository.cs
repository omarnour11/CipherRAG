using CipherRAG.Application.Interfaces;
using CipherRAG.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace CipherRAG.Infrastructure.Persistence.Repositories;

public class RoleRepository : GenericRepository<Role>, IRoleRepository
{
    public RoleRepository(AppDbContext context , IMemoryCache cache) : base(context ,cache)
    {
    }
}
