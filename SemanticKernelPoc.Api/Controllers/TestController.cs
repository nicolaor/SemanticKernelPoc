using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SemanticKernelPoc.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("public")]
    public IActionResult PublicEndpoint()
    {
        return Ok(new { message = "This is a public endpoint" });
    }

    [Authorize]
    [HttpGet("protected")]
    public IActionResult ProtectedEndpoint()
    {
        var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var userName = User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;

        return Ok(new
        {
            message = "This is a protected endpoint",
            userId,
            userName,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
}