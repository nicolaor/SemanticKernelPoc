using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SemanticKernelPoc.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    [HttpGet("profile")]
    public IActionResult GetUserProfile()
    {
        var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var userName = User.FindFirst("name")?.Value ?? User.FindFirst("preferred_username")?.Value;
        var email = User.FindFirst("preferred_username")?.Value ?? User.FindFirst("email")?.Value;
        var givenName = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname")?.Value;
        var surname = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname")?.Value;

        // Generate initials from given name and surname, or fall back to username
        string initials = "U"; // default
        if (!string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(surname))
        {
            initials = $"{givenName.First()}{surname.First()}".ToUpper();
        }
        else if (!string.IsNullOrEmpty(userName))
        {
            var parts = userName.Split(' ', '@', '.').Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (parts.Length >= 2)
            {
                initials = $"{parts[0].First()}{parts[1].First()}".ToUpper();
            }
            else if (parts.Length == 1 && parts[0].Length >= 2)
            {
                initials = parts[0].Substring(0, 2).ToUpper();
            }
        }

        return Ok(new
        {
            userId,
            userName,
            email,
            givenName,
            surname,
            initials,
            displayName = !string.IsNullOrEmpty(givenName) && !string.IsNullOrEmpty(surname) 
                ? $"{givenName} {surname}" 
                : userName ?? "User"
        });
    }

    [HttpGet("debug-claims")]
    public IActionResult GetDebugClaims()
    {
        var claims = User.Claims.Select(c => new { 
            Type = c.Type, 
            Value = c.Value 
        }).ToList();
        
        return Ok(new { 
            claims,
            claimCount = claims.Count,
            userId = User.Identity?.Name
        });
    }
} 