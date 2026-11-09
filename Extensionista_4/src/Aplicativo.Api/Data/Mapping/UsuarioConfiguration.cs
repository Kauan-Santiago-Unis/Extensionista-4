using Aplicativo.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aplicativo.Api.Data.Mapping;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuario");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GoogleSubject).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FotoUrl).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.CriadoEmUtc).IsRequired();
        builder.Property(x => x.AtualizadoEmUtc).IsRequired();
        builder.Property(x => x.UltimoLoginEmUtc).IsRequired();
        builder.HasIndex(x => x.GoogleSubject).IsUnique();
        builder.HasOne(x => x.GrupoUsuario)
            .WithMany(x => x.Usuarios)
            .HasForeignKey(x => x.GrupoUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
