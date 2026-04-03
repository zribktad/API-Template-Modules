using FileStorage.Shared;
using SharedKernel.Infrastructure.Repositories;

namespace FileStorage.Shared;

public sealed class StoredFileRepository(FileStorageDbContext dbContext)
    : RepositoryBase<StoredFile>(dbContext),
        IStoredFileRepository;


