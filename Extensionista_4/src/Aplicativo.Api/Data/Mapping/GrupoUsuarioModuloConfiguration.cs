using Aplicativo.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aplicativo.Api.Data.Mapping;

public sealed class GrupoUsuarioModuloConfiguration : IEntityTypeConfiguration<GrupoUsuarioModulo>
{
    public void Configure(EntityTypeBuilder<GrupoUsuarioModulo> builder)
    {
        builder.ToTable("GrupoUsuarioModulo");
        builder.HasKey(x => new { x.GrupoUsuarioId, x.ModuloId });
        builder.HasOne(x => x.GrupoUsuario)
            .WithMany(x => x.GrupoUsuarioModulos)
            .HasForeignKey(x => x.GrupoUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Modulo)
            .WithMany(x => x.GrupoUsuarioModulos)
            .HasForeignKey(x => x.ModuloId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
