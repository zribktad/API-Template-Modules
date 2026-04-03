using Asp.Versioning;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Contracts.Api;
using SharedKernel.Contracts.Api.Filters.Idempotency;
using SharedKernel.Contracts.Security;
using Wolverine;

namespace ProductCatalog.Features.IdempotentCreate;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that demonstrates idempotent POST semantics using the
/// <see cref="IdempotentAttribute"/> action filter to detect and short-circuit duplicate requests.
/// </summary>
public sealed class IdempotentCreateController(IMessageBus bus) : ApiControllerBase
{
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

        return Ok(result.Value);
    }
}
