using Aplicativo.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Aplicativo.Api.Data.Context;

public sealed class LogTrackDbContext(DbContextOptions<LogTrackDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<GrupoUsuario> GruposUsuario => Set<GrupoUsuario>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<GrupoUsuarioModulo> GruposUsuarioModulos => Set<GrupoUsuarioModulo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogTrackDbContext).Assembly);
    }
}
