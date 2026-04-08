using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CPCA.MailMule.Backend.Controllers;

[Route("")]
public sealed class AuthController(IConfiguration configuration) : BackendControllerBase
{
    private readonly String frontendBaseUrl = configuration["Frontend:BaseUrl"]
        ?? throw new InvalidOperationException("Frontend:BaseUrl is not configured.");

    [HttpGet("signin")]
    public async Task<IActionResult> SignInAsync()
    {
        await HttpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = frontendBaseUrl + "/" });
        return new EmptyResult();
    }

    [HttpGet("signout")]
    public async Task<IActionResult> SignOutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = frontendBaseUrl + "/" });
        return new EmptyResult();
    }

    [HttpGet("bff/secure")]
    [Authorize(Policy = "Operator")]
    public IActionResult GetSecure()
    {
        var name = User.Identity?.Name ?? "Unknown";
        return Ok(new { message = $"Hello {name}, you are authenticated." });
    }

    [HttpGet("bff/user")]
    public IActionResult GetCurrentUser()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            if (IsAjaxRequest(Request))
            {
                return new JsonResult(null)
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }

            return Unauthorized();
        }

        var name = User.Identity!.Name ?? "Unknown";

        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static role => role, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(new { name, roles });
    }
}
