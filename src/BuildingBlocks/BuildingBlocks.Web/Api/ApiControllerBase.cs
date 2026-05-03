using Microsoft.AspNetCore.Mvc;
using BuildingBlocks.Application.DTOs;

namespace BuildingBlocks.Web.Api;

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

