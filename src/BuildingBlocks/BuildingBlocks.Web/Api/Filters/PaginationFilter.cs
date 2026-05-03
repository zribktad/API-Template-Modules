using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using BuildingBlocks.Domain.Common;

namespace BuildingBlocks.Web.Api.Filters;

/// <summary>
///     A unified filter that intercepts both MVC and Minimal API/Wolverine HTTP endpoint results.
///     It appends pagination HTTP headers if the underlying response body implements <see cref="IPagedResponse" />.
///
///     Note: ASP.NET Core has two separate pipelines for returning HTTP responses.
///     <see cref="IAsyncResultFilter"/> handles classic MVC controllers.
///     <see cref="IEndpointFilter"/> handles Minimal APIs and Wolverine.
/// </summary>
public class PaginationFilter : IAsyncResultFilter, IEndpointFilter
{
    // ----- MVC Pipeline (Controllers) -----
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next
    )
    {
        if (context.Result is ObjectResult { Value: IPagedResponse pagedResponse })
        {
            PaginationHeaderHelper.AppendPaginationHeaders(
                context.HttpContext.Response,
                pagedResponse
            );
        }

        await next();
    }

    // ----- Minimal API / Wolverine Pipeline -----
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        object? result = await next(context);

        if (result is IPagedResponse directResponse)
        {
            PaginationHeaderHelper.AppendPaginationHeaders(
                context.HttpContext.Response,
                directResponse
            );
        }
        else if (result is IValueHttpResult { Value: IPagedResponse wrappedResponse })
        {
            PaginationHeaderHelper.AppendPaginationHeaders(
                context.HttpContext.Response,
                wrappedResponse
            );
        }

        return result;
    }
}

