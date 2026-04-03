using FileStorage.Contracts;
using FileStorage.Domain;
using FileStorage.Repositories;
using FileStorage.Services;
using FileStorage.Errors;
using SharedKernel.Infrastructure.Repositories;

namespace FileStorage.Domain;

public sealed class StoredFileRepository(FileStorageDbContext dbContext)
    : RepositoryBase<StoredFile>(dbContext),
        IStoredFileRepository;






