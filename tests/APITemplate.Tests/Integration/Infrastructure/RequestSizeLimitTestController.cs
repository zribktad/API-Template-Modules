using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Tests.Integration.Infrastructure;

[ApiController]
[Route("test/request-size")]
[AllowAnonymous]
public class RequestSizeLimitTestController : ControllerBase
{
    [HttpPost("global-limit")]
    public IActionResult PostWithGlobalLimit([FromBody] string body)
    {
        return Ok(body);
    }

    [HttpPost("disable-limit")]
    [DisableRequestSizeLimit]
    public IActionResult PostWithDisabledLimit([FromBody] string body)
    {
        return Ok(body);
    }

    [HttpPost("custom-limit")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB custom limit
    public IActionResult PostWithCustomLimit([FromBody] string body)
    {
        return Ok(body);
    }
}
