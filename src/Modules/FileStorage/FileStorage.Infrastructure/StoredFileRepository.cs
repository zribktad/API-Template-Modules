using FileStorage.Domain;
using SharedKernel.Infrastructure.Repositories;

namespace FileStorage.Infrastructure;

public sealed class StoredFileRepository(FileStorageDbContext dbContext)
    : RepositoryBase<StoredFile>(dbContext),
        IStoredFileRepository;
