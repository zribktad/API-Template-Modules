using Microsoft.EntityFrameworkCore;

namespace APITemplate.Api.ExceptionHandling;

internal sealed record MappedApiError(
    int StatusCode,
    string Title,
    string Detail,
    string ErrorCode,
    IReadOnlyDictionary<string, object>? Metadata
);

internal static class ApiExceptionMapper
{
    public static bool TryMap(Exception exception, out MappedApiError mapped)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            mapped = new MappedApiError(
                StatusCodes.Status409Conflict,
                "Conflict",
                "The resource was modified by another request. Please retrieve the latest version and retry.",
                ErrorCatalog.General.ConcurrencyConflict,
                null
            );
            return true;
        }

        mapped = default!;
        return false;
    }
}
