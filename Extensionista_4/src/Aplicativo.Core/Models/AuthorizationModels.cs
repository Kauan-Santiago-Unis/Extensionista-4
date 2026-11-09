namespace Aplicativo.Core.Models;

public enum StatusUsuario
{
    AguardandoAprovacao,
    Ativo,
    Inativo
}

public sealed class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string GoogleSubject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? FotoUrl { get; set; }
    public int? GrupoUsuarioId { get; set; }
    public GrupoUsuario? GrupoUsuario { get; set; }
    public StatusUsuario Status { get; set; } = StatusUsuario.AguardandoAprovacao;
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime UltimoLoginEmUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GrupoUsuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEmUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<GrupoUsuarioModulo> GrupoUsuarioModulos { get; set; } = new List<GrupoUsuarioModulo>();
}

public sealed class Modulo
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEmUtc { get; set; } = DateTime.UtcNow;
    public ICollection<GrupoUsuarioModulo> GrupoUsuarioModulos { get; set; } = new List<GrupoUsuarioModulo>();
}

public sealed class GrupoUsuarioModulo
{
    public int GrupoUsuarioId { get; set; }
    public GrupoUsuario GrupoUsuario { get; set; } = null!;
    public int ModuloId { get; set; }
    public Modulo Modulo { get; set; } = null!;
}
