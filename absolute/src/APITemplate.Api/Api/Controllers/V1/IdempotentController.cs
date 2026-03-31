using APITemplate.Api.Authorization;
using APITemplate.Api.Controllers;
using APITemplate.Api.ErrorOrMapping;
using APITemplate.Api.Filters.Idempotency;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.Examples;
using APITemplate.Application.Features.Examples.DTOs;
using Asp.Versioning;
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
    /// <summary>
    /// Creates a resource idempotently; repeated requests with the same idempotency key
    /// return the original response without re-executing the command.
    /// </summary>
    [HttpPost]
    [Idempotent]
    [RequirePermission(Permission.Examples.Create)]
    public async Task<ActionResult<IdempotentCreateResponse>> Create(
        IdempotentCreateRequest request,
        CancellationToken ct
    )
    {
        var result = await bus.InvokeAsync<ErrorOr<IdempotentCreateResponse>>(
            new IdempotentCreateCommand(request),
            ct
        );
        if (result.IsError)
            return result.ToActionResult(this);

        return Created(string.Empty, result.Value);
    }
}
