using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using SharedKernel.Domain.Common;

namespace SharedKernel.Contracts.Api;

public static class PaginationHeaderHelper
{
    public static void AppendLinkHeader(HttpResponse response, IPagedResponse pagedResponse)
    {
        var request = response.HttpContext.Request;
        var links = new List<string>();

        string baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        var parsedQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(
            request.QueryString.Value
        );

        if (pagedResponse.HasNextPage)
        {
            links.Add(CreateLink(baseUrl, parsedQuery, pagedResponse.PageNumber + 1, "next"));
        }

        if (pagedResponse.HasPreviousPage)
        {
            links.Add(CreateLink(baseUrl, parsedQuery, pagedResponse.PageNumber - 1, "prev"));
        }

        links.Add(CreateLink(baseUrl, parsedQuery, 1, "first"));
        links.Add(
            CreateLink(
                baseUrl,
                parsedQuery,
                pagedResponse.TotalPages > 0 ? pagedResponse.TotalPages : 1,
                "last"
            )
        );

        response.Headers.Append("Link", string.Join(", ", links));

        response.Headers.Append("X-Pagination-Total-Count", pagedResponse.TotalCount.ToString());
        response.Headers.Append("X-Pagination-Total-Pages", pagedResponse.TotalPages.ToString());
        response.Headers.Append("X-Pagination-Page-Number", pagedResponse.PageNumber.ToString());
        response.Headers.Append("X-Pagination-Page-Size", pagedResponse.PageSize.ToString());

        response.Headers.Append(
            "Access-Control-Expose-Headers",
            "Link, X-Pagination-Total-Count, X-Pagination-Total-Pages, X-Pagination-Page-Number, X-Pagination-Page-Size"
        );
    }

    private static string CreateLink(
        string baseUrl,
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query,
        int pageNumber,
        string rel
    )
    {
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(query)
        {
            ["pageNumber"] = pageNumber.ToString(),
        };

        string fullUrl = QueryHelpers.AddQueryString(baseUrl, dict);

        return $"<{fullUrl}>; rel=\"{rel}\"";
    }
}
