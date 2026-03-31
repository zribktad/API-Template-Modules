using SharedKernel.Domain.Interfaces;

namespace FileStorage.Domain;

/// <summary>
/// Repository contract for <see cref="StoredFile"/> entities, inheriting all generic CRUD operations from <see cref="IRepository{T}"/>.
/// </summary>
public interface IStoredFileRepository : IRepository<StoredFile>;
