using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Ardalis.Specification.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

/// <summary>EF Core repository for <see cref="StoredFile"/>, inheriting all standard CRUD and specification query support from <see cref="RepositoryBase{T}"/>.</summary>
public sealed class StoredFileRepository : RepositoryBase<StoredFile>, IStoredFileRepository
{
    public StoredFileRepository(AppDbContext dbContext)
        : base(dbContext) { }
}
