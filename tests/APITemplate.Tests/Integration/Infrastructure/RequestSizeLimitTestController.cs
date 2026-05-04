using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Tests.Integration.Infrastructure;

[ApiController]
[Route("test/request-size")]
[AllowAnonymous]
public class RequestSizeLimitTestController : ControllerBase
{
    [HttpPost("global-limit")]
    public IActionResult PostWithGlobalLimit()
    {
        return Ok("Global limit applied");
    }

    [HttpPost("disable-limit")]
    [DisableRequestSizeLimit]
    public IActionResult PostWithDisabledLimit()
    {
        return Ok("Limit disabled");
    }

    [HttpPost("custom-limit")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB custom limit
    public IActionResult PostWithCustomLimit()
    {
        return Ok("Custom limit applied");
    }
}
