namespace APITemplate.Infrastructure.Repositories.Pagination;

/// <summary>
/// Internal wrapper that carries a projected item together with the total count
/// so that both can be retrieved in a single SQL query via a scalar sub-query.
/// </summary>
internal sealed record PagedRow<TResult>(TResult Item, int TotalCount);
