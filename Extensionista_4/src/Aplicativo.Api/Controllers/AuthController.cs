using Aplicativo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Aplicativo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(GoogleTokenService googleTokenService, JwtTokenService jwtTokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("google")]
    public async Task<ActionResult<GoogleLoginResponse>> Google(
        [FromBody] GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var googleUser = await googleTokenService.ValidateAsync(request.IdToken, request.Nonce, cancellationToken);
            var appToken = jwtTokenService.Create(googleUser);

            return Ok(new GoogleLoginResponse(
                appToken.Token,
                appToken.ExpiresAt,
                new UserResponse(googleUser.Subject, googleUser.Email, googleUser.Name, googleUser.Picture)));
        }
        catch (Google.Apis.Auth.InvalidJwtException)
        {
            return Unauthorized(new { message = "O token do Google é inválido ou expirou." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<UserResponse> Me()
    {
        return Ok(new UserResponse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? string.Empty,
            User.Identity?.Name ?? string.Empty,
            User.FindFirstValue("picture") ?? string.Empty));
    }
}

public sealed record GoogleLoginRequest(string IdToken, string Nonce);
public sealed record GoogleLoginResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);
public sealed record UserResponse(string Id, string Email, string Name, string Picture);
