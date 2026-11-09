using Aplicativo.Api.Data.Context;
using Aplicativo.Api.Options;
using Aplicativo.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aplicativo.Api.Services;

public sealed class AuthorizationBootstrapService(
    LogTrackDbContext db,
    IOptions<BootstrapOptions> bootstrapOptions)
{
    private readonly LogTrackDbContext _db = db;
    private readonly BootstrapOptions _options = bootstrapOptions.Value;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await _db.Database.MigrateAsync(cancellationToken);

        var adminGroup = await _db.GruposUsuario
            .Include(x => x.GrupoUsuarioModulos)
            .SingleOrDefaultAsync(x => x.Nome == AuthorizationCatalog.AdministratorGroupName, cancellationToken);

        if (adminGroup is null)
        {
            adminGroup = new GrupoUsuario
            {
                Nome = AuthorizationCatalog.AdministratorGroupName,
                Descricao = "Grupo administrativo inicial do LogTrack",
                Ativo = true,
                CriadoEmUtc = DateTime.UtcNow,
                AtualizadoEmUtc = DateTime.UtcNow
            };
            _db.GruposUsuario.Add(adminGroup);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var modules = await _db.Modulos.ToDictionaryAsync(x => x.Codigo, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var modulesChanged = false;
        foreach (var seed in AuthorizationCatalog.InitialModules)
        {
            if (!modules.TryGetValue(seed.Code, out var module))
            {
                module = new Modulo
                {
                    Codigo = seed.Code,
                    Nome = seed.Name,
                    Descricao = seed.Description,
                    Ativo = true,
                    CriadoEmUtc = DateTime.UtcNow,
                    AtualizadoEmUtc = DateTime.UtcNow
                };
                _db.Modulos.Add(module);
                modules[seed.Code] = module;
                modulesChanged = true;
            }
        }

        if (modulesChanged)
        {
            await _db.SaveChangesAsync(cancellationToken);
            adminGroup = await _db.GruposUsuario
                .Include(x => x.GrupoUsuarioModulos)
                .SingleAsync(x => x.Id == adminGroup.Id, cancellationToken);
        }

        var associationsChanged = false;
        foreach (var seed in AuthorizationCatalog.InitialModules)
        {
            var module = modules[seed.Code];
            if (!adminGroup.GrupoUsuarioModulos.Any(x => x.ModuloId == module.Id))
            {
                _db.GruposUsuarioModulos.Add(new GrupoUsuarioModulo
                {
                    GrupoUsuarioId = adminGroup.Id,
                    ModuloId = module.Id
                });
                associationsChanged = true;
            }
        }

        if (associationsChanged)
            await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyFirstAdministratorAsync(Usuario user, CancellationToken cancellationToken)
    {
        var configuredEmail = _options.AdminEmail.Trim();
        if (string.IsNullOrWhiteSpace(configuredEmail) ||
            !string.Equals(user.Email, configuredEmail, StringComparison.OrdinalIgnoreCase) ||
            user.Status != StatusUsuario.AguardandoAprovacao ||
            user.GrupoUsuarioId is not null)
            return;

        var adminGroup = await _db.GruposUsuario
            .SingleAsync(x => x.Nome == AuthorizationCatalog.AdministratorGroupName, cancellationToken);

        user.Status = StatusUsuario.Ativo;
        user.GrupoUsuarioId = adminGroup.Id;
        user.AtualizadoEmUtc = DateTime.UtcNow;
    }
}
