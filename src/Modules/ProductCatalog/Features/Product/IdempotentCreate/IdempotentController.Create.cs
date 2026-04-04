using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Features.Shared.Routing;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Api.Filters.Idempotency;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.Product.IdempotentCreate;

public sealed partial class IdempotentController
{
    /// <summary>
    /// Demonstrates idempotent POST semantics using the
    /// <see cref="IdempotentAttribute"/> filter for duplicate requests.
    /// </summary>
    [HttpPost]
    [Idempotent]
    [RequirePermission(Permission.Examples.Create)]
    public async Task<ActionResult<IdempotentCreateResponse>> Create(
        IdempotentCreateRequest request,
        CancellationToken ct
    )
    {
        ErrorOr<IdempotentCreateResponse> result = await bus.InvokeAsync<
            ErrorOr<IdempotentCreateResponse>
        >(new IdempotentCreateCommand(request), ct);
        if (result.IsError)
            return result.ToActionResult(this);

        return Created(
            $"/api/v{this.GetApiVersion()}/{ProductCatalogRouteTemplates.IdempotentPathSegment}",
            result.Value
        );
    }
}
