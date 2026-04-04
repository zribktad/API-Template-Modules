using Microsoft.AspNetCore.Mvc;
using SharedKernel.Application.DTOs;

namespace SharedKernel.Contracts.Api;

[ApiController]
// ReSharper disable once RouteTemplates.RouteParameterConstraintNotResolved
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    internal ActionResult<BatchResponse> OkOrUnprocessable(BatchResponse response)
    {
        return response.FailureCount > 0 ? UnprocessableEntity(response) : Ok(response);
    }
}
