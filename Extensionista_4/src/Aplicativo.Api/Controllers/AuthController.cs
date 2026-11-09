using Aplicativo.Api.Services;
using Aplicativo.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Aplicativo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    GoogleTokenService googleTokenService,
    UserAuthorizationService userAuthorizationService,
    JwtTokenService jwtTokenService) : ControllerBase
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
            var user = await userAuthorizationService.SignInWithGoogleAsync(googleUser, cancellationToken);
            var appToken = jwtTokenService.Create(user);

            return Ok(new GoogleLoginResponse(
                appToken.Token,
                appToken.ExpiresAt,
                UserResponse.From(user)));
        }
        catch (Google.Apis.Auth.InvalidJwtException)
        {
            return Unauthorized(new { message = "O token do Google é inválido ou expirou." });
        }
        catch (UserInactiveException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
            return Unauthorized(new { message = "A sessão não possui uma identidade de usuário válida." });

        var user = await userAuthorizationService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Unauthorized(new { message = "O usuário da sessão não foi encontrado." });
        if (user.Status == StatusUsuario.Inativo)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "O usuário está inativo." });

        return Ok(UserResponse.From(user));
    }
}

public sealed record GoogleLoginRequest(string IdToken, string Nonce);
public sealed record GoogleLoginResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);
public sealed record UserResponse(
    string Id,
    string Email,
    string Name,
    string Picture,
    string Status,
    GroupResponse? Group,
    IReadOnlyList<ModuleResponse> Modules)
{
    public static UserResponse From(Usuario user)
    {
        var modules = user.GrupoUsuario?.GrupoUsuarioModulos
            .Where(x => x.Modulo.Ativo)
            .Select(x => new ModuleResponse(x.Modulo.Codigo, x.Modulo.Nome))
            .DistinctBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Code)
            .ToArray() ?? [];

        var group = user.GrupoUsuario is null
            ? null
            : new GroupResponse(user.GrupoUsuario.Id, user.GrupoUsuario.Nome);

        return new UserResponse(
            user.Id.ToString(),
            user.Email,
            user.Nome,
            user.FotoUrl ?? string.Empty,
            user.Status.ToString(),
            group,
            modules);
    }
}

public sealed record GroupResponse(int Id, string Name);
public sealed record ModuleResponse(string Code, string Name);
