using APITemplate.Api.Features.Examples.Commands;
using APITemplate.Api.Features.Examples.DTOs;
using APITemplate.Api.Filters.Idempotency;
using Asp.Versioning;
using Contracts.Api;
using Contracts.Security;
using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
/// <summary>
/// Presentation-layer controller that demonstrates idempotent POST semantics using the
/// <see cref="Idempotent"/> action filter to detect and short-circuit duplicate requests.
/// </summary>
public sealed class IdempotentController(IMessageBus bus) : ApiControllerBase
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

        return Created(string.Empty, result.Value);
    }
}
