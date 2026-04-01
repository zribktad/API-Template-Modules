using Microsoft.AspNetCore.Mvc;
using SharedKernel.Application.DTOs;

namespace Contracts.Api;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    internal ActionResult<BatchResponse> OkOrUnprocessable(BatchResponse response) =>
        response.FailureCount > 0 ? UnprocessableEntity(response) : Ok(response);
}
