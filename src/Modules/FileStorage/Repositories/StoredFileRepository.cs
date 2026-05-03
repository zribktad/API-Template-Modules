using BuildingBlocks.Infrastructure.EFCore.Repositories;
using FileStorage.Persistence;

namespace FileStorage.Domain;

public sealed class StoredFileRepository(FileStorageDbContext dbContext)
    : RepositoryBase<StoredFile>(dbContext),
        IStoredFileRepository;
