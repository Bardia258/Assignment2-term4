using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MunicipalElections.Web.Models;

namespace MunicipalElections.Web.Controllers.Api;

// Uses the original SQLite/Identity backend and its existing JWT validation.
[ApiController, Route("api/auth")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthSessionController(UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> CurrentUser()
    {
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var user = await users.FindByIdAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email)) return Unauthorized();
        return Ok(new { id = user.Id, email = user.Email, username = user.UserName });
    }
}
