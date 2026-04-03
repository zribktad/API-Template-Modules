using FileStorage.Persistence;
using SharedKernel.Infrastructure.Repositories;

namespace FileStorage.Domain;

public sealed class StoredFileRepository(FileStorageDbContext dbContext)
    : RepositoryBase<StoredFile>(dbContext),
        IStoredFileRepository;
