using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Application.Errors;
using SharedKernel.Domain.Common;

namespace SharedKernel.Infrastructure.Repositories.Pagination;

internal static class PagedQueryExecutor
{
    internal static async Task<ErrorOr<PagedResponse<TResult>>> ExecuteAsync<T, TResult>(
        IQueryable<T> baseQuery,
        IQueryable<T> countSource,
        Expression<Func<T, TResult>> selector,
        int pageNumber,
        int pageSize,
        CancellationToken ct
    )
    {
        if (pageNumber < 1)
            return Error.Validation(
                ErrorCatalog.General.PageOutOfRange,
                "Page number must be at least 1."
            );
        if (pageSize < 1)
            return Error.Validation(
                ErrorCatalog.General.PageOutOfRange,
                "Page size must be at least 1."
            );

        int skip = (pageNumber - 1) * pageSize;
        int totalCount = await countSource.CountAsync(ct);

        if (totalCount == 0)
            return new PagedResponse<TResult>([], 0, pageNumber, pageSize);

        if (skip >= totalCount)
        {
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return Error.Validation(
                ErrorCatalog.General.PageOutOfRange,
                $"PageNumber {pageNumber} exceeds total pages ({totalPages})."
            );
        }

        List<TResult> items = await baseQuery
            .Skip(skip)
            .Take(pageSize)
            .Select(selector)
            .ToListAsync(ct);

        return new PagedResponse<TResult>(items, totalCount, pageNumber, pageSize);
    }
}
