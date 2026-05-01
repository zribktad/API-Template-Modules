using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel.Application.Http;

namespace APITemplate.Tests.Integration.Infrastructure;

[ApiController]
[Route("test/rate-limit")]
[AllowAnonymous]
public class RateLimitTestController : ControllerBase
{
    [HttpGet("no-policy-1")]
    public IActionResult GetNoPolicy1() => Ok("Global 1");

    [HttpGet("no-policy-2")]
    public IActionResult GetNoPolicy2() => Ok("Global 2");

    [HttpGet("fixed-1")]
    [EnableRateLimiting("fixed-test-1")]
    public IActionResult GetFixed1() => Ok("Fixed 1");

    [HttpGet("fixed-2")]
    [EnableRateLimiting("fixed-test-2")]
    public IActionResult GetFixed2() => Ok("Fixed 2");

    [HttpGet("sliding-1")]
    [EnableRateLimiting("sliding-test-1")]
    public IActionResult GetSliding1() => Ok("Sliding 1");
}
