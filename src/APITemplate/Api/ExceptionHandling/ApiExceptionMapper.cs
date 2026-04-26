using Microsoft.EntityFrameworkCore;

namespace APITemplate.Api.ExceptionHandling;

internal static class ApiExceptionMapper
{
    public static bool TryMap(
        Exception exception,
        out (
            int StatusCode,
            string Title,
            string Detail,
            string ErrorCode,
            IReadOnlyDictionary<string, object>? Metadata
        ) mapped
    )
    {
        if (exception is DbUpdateConcurrencyException)
        {
            mapped = (
                StatusCodes.Status409Conflict,
                "Conflict",
                "The resource was modified by another request. Please retrieve the latest version and retry.",
                ErrorCatalog.General.ConcurrencyConflict,
                null
            );
            return true;
        }

        if (exception is DbUpdateException)
        {
            mapped = (
                StatusCodes.Status409Conflict,
                "Conflict",
                "The request conflicts with the current state of the resource.",
                ErrorCatalog.General.Conflict,
                null
            );
            return true;
        }

        mapped = default;
        return false;
    }
}
