using Aplicativo.Api.Authorization;
using Aplicativo.Api.Data.Context;
using Aplicativo.Api.Services;
using Aplicativo.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aplicativo.Api.Controllers;

[ApiController]
[Route("api/authorization")]
[RequireModule("ADMINISTRACAO")]
public sealed class AuthorizationController(
    LogTrackDbContext db,
    UserAuthorizationService users) : ControllerBase
{
    [HttpGet("groups")]
    public async Task<ActionResult<IReadOnlyList<GroupDetailsResponse>>> Groups(CancellationToken cancellationToken)
    {
        var groups = await db.GruposUsuario
            .AsNoTracking()
            .Include(x => x.GrupoUsuarioModulos)
                .ThenInclude(x => x.Modulo)
            .OrderBy(x => x.Nome)
            .ToListAsync(cancellationToken);

        return Ok(groups.Select(ToResponse).ToArray());
    }

    [HttpPost("groups")]
    public async Task<ActionResult<GroupDetailsResponse>> CreateGroup(
        [FromBody] SaveGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "O nome do grupo é obrigatório." });

        if (await db.GruposUsuario.AnyAsync(x => x.Nome == request.Name.Trim(), cancellationToken))
            return Conflict(new { message = "Já existe um grupo com esse nome." });

        var now = DateTime.UtcNow;
        var group = new GrupoUsuario
        {
            Nome = request.Name.Trim(),
            Descricao = request.Description?.Trim(),
            Ativo = request.Active,
            CriadoEmUtc = now,
            AtualizadoEmUtc = now
        };
        db.GruposUsuario.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Groups), ToResponse(group));
    }

    [HttpPut("groups/{id:int}")]
    public async Task<ActionResult<GroupDetailsResponse>> UpdateGroup(
        int id,
        [FromBody] SaveGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await db.GruposUsuario.FindAsync([id], cancellationToken);
        if (group is null)
            return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "O nome do grupo é obrigatório." });
        if (await db.GruposUsuario.AnyAsync(x => x.Id != id && x.Nome == request.Name.Trim(), cancellationToken))
            return Conflict(new { message = "Já existe um grupo com esse nome." });

        group.Nome = request.Name.Trim();
        group.Descricao = request.Description?.Trim();
        group.Ativo = request.Active;
        group.AtualizadoEmUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new GroupDetailsResponse(
            group.Id,
            group.Nome,
            group.Descricao,
            group.Ativo,
            []));
    }

    [HttpPut("groups/{id:int}/modules")]
    public async Task<ActionResult<GroupDetailsResponse>> SetGroupModules(
        int id,
        [FromBody] SetGroupModulesRequest request,
        CancellationToken cancellationToken)
    {
        var group = await db.GruposUsuario
            .Include(x => x.GrupoUsuarioModulos)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
            return NotFound();

        var moduleIds = request.ModuleIds.Distinct().ToArray();
        var existingModuleIds = await db.Modulos
            .Where(x => moduleIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        if (existingModuleIds.Length != moduleIds.Length)
            return BadRequest(new { message = "Um ou mais módulos informados não existem." });

        db.GruposUsuarioModulos.RemoveRange(group.GrupoUsuarioModulos);
        foreach (var moduleId in existingModuleIds)
        {
            db.GruposUsuarioModulos.Add(new GrupoUsuarioModulo
            {
                GrupoUsuarioId = group.Id,
                ModuloId = moduleId
            });
        }

        group.AtualizadoEmUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var response = await db.GruposUsuario
            .AsNoTracking()
            .Include(x => x.GrupoUsuarioModulos)
                .ThenInclude(x => x.Modulo)
            .SingleAsync(x => x.Id == id, cancellationToken);
        return Ok(ToResponse(response));
    }

    [HttpGet("modules")]
    public async Task<ActionResult<IReadOnlyList<ModuleAdminResponse>>> Modules(CancellationToken cancellationToken)
    {
        var modules = await db.Modulos
            .AsNoTracking()
            .OrderBy(x => x.Codigo)
            .Select(x => new ModuleAdminResponse(x.Id, x.Codigo, x.Nome, x.Descricao, x.Ativo))
            .ToArrayAsync(cancellationToken);
        return Ok(modules);
    }

    [HttpPost("modules")]
    public async Task<ActionResult<ModuleAdminResponse>> CreateModule(
        [FromBody] SaveModuleRequest request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Código e nome do módulo são obrigatórios." });
        if (await db.Modulos.AnyAsync(x => x.Codigo == code, cancellationToken))
            return Conflict(new { message = "Já existe um módulo com esse código." });

        var now = DateTime.UtcNow;
        var module = new Modulo
        {
            Codigo = code,
            Nome = request.Name.Trim(),
            Descricao = request.Description?.Trim(),
            Ativo = request.Active,
            CriadoEmUtc = now,
            AtualizadoEmUtc = now
        };
        db.Modulos.Add(module);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Modules), new ModuleAdminResponse(module.Id, module.Codigo, module.Nome, module.Descricao, module.Ativo));
    }

    [HttpPut("modules/{id:int}")]
    public async Task<ActionResult<ModuleAdminResponse>> UpdateModule(
        int id,
        [FromBody] SaveModuleRequest request,
        CancellationToken cancellationToken)
    {
        var module = await db.Modulos.FindAsync([id], cancellationToken);
        if (module is null)
            return NotFound();

        var code = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Código e nome do módulo são obrigatórios." });
        if (await db.Modulos.AnyAsync(x => x.Id != id && x.Codigo == code, cancellationToken))
            return Conflict(new { message = "Já existe um módulo com esse código." });

        module.Codigo = code;
        module.Nome = request.Name.Trim();
        module.Descricao = request.Description?.Trim();
        module.Ativo = request.Active;
        module.AtualizadoEmUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ModuleAdminResponse(module.Id, module.Codigo, module.Nome, module.Descricao, module.Ativo));
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AuthorizationUserResponse>>> Users(CancellationToken cancellationToken)
    {
        var result = await users.QueryUsers()
            .OrderBy(x => x.Nome)
            .Select(x => new AuthorizationUserResponse(
                x.Id,
                x.Email,
                x.Nome,
                x.Status.ToString(),
                x.GrupoUsuario == null ? null : new GroupResponse(x.GrupoUsuario.Id, x.GrupoUsuario.Nome)))
            .ToArrayAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("users/{id:guid}/access")]
    public async Task<ActionResult<AuthorizationUserResponse>> UpdateUserAccess(
        Guid id,
        [FromBody] UpdateUserAccessRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Usuarios
            .Include(x => x.GrupoUsuario)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return NotFound();
        if (!Enum.TryParse<StatusUsuario>(request.Status, true, out var status))
            return BadRequest(new { message = "StatusUsuario inválido." });

        GrupoUsuario? group = null;
        if (request.GroupId.HasValue)
        {
            group = await db.GruposUsuario.FindAsync([request.GroupId.Value], cancellationToken);
            if (group is null)
                return BadRequest(new { message = "O grupo informado não existe." });
        }

        user.Status = status;
        user.GrupoUsuarioId = group?.Id;
        user.AtualizadoEmUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new AuthorizationUserResponse(
            user.Id,
            user.Email,
            user.Nome,
            user.Status.ToString(),
            group is null ? null : new GroupResponse(group.Id, group.Nome)));
    }

    private static GroupDetailsResponse ToResponse(GrupoUsuario group) => new(
        group.Id,
        group.Nome,
        group.Descricao,
        group.Ativo,
        group.GrupoUsuarioModulos
            .Where(x => x.Modulo is not null)
            .Select(x => new ModuleAdminResponse(x.Modulo.Id, x.Modulo.Codigo, x.Modulo.Nome, x.Modulo.Descricao, x.Modulo.Ativo))
            .OrderBy(x => x.Code)
            .ToArray());
}

public sealed record SaveGroupRequest(string Name, string? Description, bool Active = true);
public sealed record SetGroupModulesRequest(IReadOnlyCollection<int> ModuleIds);
public sealed record SaveModuleRequest(string Code, string Name, string? Description, bool Active = true);
public sealed record UpdateUserAccessRequest(string Status, int? GroupId);
public sealed record GroupDetailsResponse(int Id, string Name, string? Description, bool Active, IReadOnlyList<ModuleAdminResponse> Modules);
public sealed record ModuleAdminResponse(int Id, string Code, string Name, string? Description, bool Active);
public sealed record AuthorizationUserResponse(Guid Id, string Email, string Name, string Status, GroupResponse? Group);
