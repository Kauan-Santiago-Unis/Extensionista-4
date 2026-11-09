using Aplicativo.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aplicativo.Api.Data.Mapping;

public sealed class GrupoUsuarioConfiguration : IEntityTypeConfiguration<GrupoUsuario>
{
    public void Configure(EntityTypeBuilder<GrupoUsuario> builder)
    {
        builder.ToTable("GrupoUsuario");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500);
        builder.Property(x => x.Ativo).IsRequired();
        builder.Property(x => x.CriadoEmUtc).IsRequired();
        builder.Property(x => x.AtualizadoEmUtc).IsRequired();
        builder.HasIndex(x => x.Nome).IsUnique();
    }
}
