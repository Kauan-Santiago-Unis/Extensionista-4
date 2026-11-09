using Aplicativo.Api.Data.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Aplicativo.Api.Authorization;

public static class ModuleAuthorization
{
    public const string PolicyPrefix = "Modulo:";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireModuleAttribute : AuthorizeAttribute
{
    public string ModuleCode { get; }

    public RequireModuleAttribute(string moduleCode)
    {
        ModuleCode = moduleCode.Trim().ToUpperInvariant();
        Policy = $"{ModuleAuthorization.PolicyPrefix}{ModuleCode}";
    }
}

public sealed class ModuleRequirement(string moduleCode) : IAuthorizationRequirement
{
    public string ModuleCode { get; } = moduleCode.Trim().ToUpperInvariant();
}

public sealed class ModuleAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(ModuleAuthorization.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return base.GetPolicyAsync(policyName);

        var moduleCode = policyName[ModuleAuthorization.PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new ModuleRequirement(moduleCode))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public sealed class ModuleAuthorizationHandler(LogTrackDbContext db)
    : AuthorizationHandler<ModuleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModuleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
            return;

        var allowed = await db.Usuarios
            .AsNoTracking()
            .Where(x => x.Id == userId && x.Status == Aplicativo.Core.Models.StatusUsuario.Ativo)
            .Where(x => x.GrupoUsuario != null && x.GrupoUsuario.Ativo)
            .SelectMany(x => x.GrupoUsuario!.GrupoUsuarioModulos)
            .AnyAsync(x => x.Modulo.Ativo && x.Modulo.Codigo == requirement.ModuleCode);

        if (allowed)
            context.Succeed(requirement);
    }
}
