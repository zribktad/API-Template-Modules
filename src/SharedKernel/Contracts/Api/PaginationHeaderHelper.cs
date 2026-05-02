using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using SharedKernel.Domain.Common;

namespace SharedKernel.Contracts.Api;

public static class PaginationHeaderHelper
{
    public static void AppendPaginationHeaders(HttpResponse response, IPagedResponse pagedResponse)
    {
        HttpRequest request = response.HttpContext.Request;
        List<string> links = new List<string>();

        string baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        Dictionary<string, StringValues> parsedQuery = QueryHelpers.ParseQuery(
            request.QueryString.Value
        );

        if (pagedResponse.HasNextPage)
        {
            links.Add(
                CreateLink(
                    baseUrl,
                    parsedQuery,
                    pagedResponse.PageNumber + 1,
                    pagedResponse.PageSize,
                    "next"
                )
            );
        }

        if (pagedResponse.HasPreviousPage)
        {
            links.Add(
                CreateLink(
                    baseUrl,
                    parsedQuery,
                    pagedResponse.PageNumber - 1,
                    pagedResponse.PageSize,
                    "prev"
                )
            );
        }

        links.Add(CreateLink(baseUrl, parsedQuery, 1, pagedResponse.PageSize, "first"));

        if (pagedResponse.TotalPages > 0)
        {
            links.Add(
                CreateLink(
                    baseUrl,
                    parsedQuery,
                    pagedResponse.TotalPages,
                    pagedResponse.PageSize,
                    "last"
                )
            );
        }

        response.Headers["Link"] = string.Join(", ", links);

        response.Headers["X-Pagination-Total-Count"] = pagedResponse.TotalCount.ToString();
        response.Headers["X-Pagination-Total-Pages"] = pagedResponse.TotalPages.ToString();
        response.Headers["X-Pagination-Page-Number"] = pagedResponse.PageNumber.ToString();
        response.Headers["X-Pagination-Page-Size"] = pagedResponse.PageSize.ToString();

        const string exposeHeaders =
            "Link, X-Pagination-Total-Count, X-Pagination-Total-Pages, X-Pagination-Page-Number, X-Pagination-Page-Size";
        if (
            response.Headers.TryGetValue("Access-Control-Expose-Headers", out StringValues existing)
        )
        {
            string merged = string.Join(", ", existing.ToString(), exposeHeaders);
            response.Headers["Access-Control-Expose-Headers"] = merged;
        }
        else
        {
            response.Headers["Access-Control-Expose-Headers"] = exposeHeaders;
        }
    }

    private static string CreateLink(
        string baseUrl,
        Dictionary<string, StringValues> query,
        int pageNumber,
        int pageSize,
        string rel
    )
    {
        Dictionary<string, StringValues> dict = new Dictionary<string, StringValues>(query)
        {
            ["pageNumber"] = pageNumber.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };

        string fullUrl = QueryHelpers.AddQueryString(baseUrl, dict);

        return $"<{fullUrl}>; rel=\"{rel}\"";
    }
}
