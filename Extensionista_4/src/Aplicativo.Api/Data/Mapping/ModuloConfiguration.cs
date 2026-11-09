using Aplicativo.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aplicativo.Api.Data.Mapping;

public sealed class ModuloConfiguration : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> builder)
    {
        builder.ToTable("Modulo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Codigo).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Descricao).HasMaxLength(500);
        builder.Property(x => x.Ativo).IsRequired();
        builder.Property(x => x.CriadoEmUtc).IsRequired();
        builder.Property(x => x.AtualizadoEmUtc).IsRequired();
        builder.HasIndex(x => x.Codigo).IsUnique();
    }
}
