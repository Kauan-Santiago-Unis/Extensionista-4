using Aplicativo.Api.Data.Context;
using Aplicativo.Core.Models;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;

namespace Aplicativo.Api.Services;

public sealed class UserInactiveException : UnauthorizedAccessException
{
    public UserInactiveException()
        : base("O usuário está inativo e não pode iniciar uma nova sessão.")
    {
    }
}

public sealed class UserAuthorizationService(
    LogTrackDbContext db,
    AuthorizationBootstrapService bootstrap)
{
    private readonly LogTrackDbContext _db = db;
    private readonly AuthorizationBootstrapService _bootstrap = bootstrap;

    public async Task<Usuario> SignInWithGoogleAsync(
        GoogleJsonWebSignature.Payload googleUser,
        CancellationToken cancellationToken)
    {
        await _bootstrap.EnsureInitializedAsync(cancellationToken);

        var user = await _db.Usuarios
            .SingleOrDefaultAsync(x => x.GoogleSubject == googleUser.Subject, cancellationToken);

        if (user is not null && user.Status == StatusUsuario.Inativo)
            throw new UserInactiveException();

        var now = DateTime.UtcNow;
        if (user is null)
        {
            user = new Usuario
            {
                GoogleSubject = googleUser.Subject,
                Email = googleUser.Email,
                Nome = googleUser.Name ?? googleUser.Email,
                FotoUrl = googleUser.Picture,
                Status = StatusUsuario.AguardandoAprovacao,
                CriadoEmUtc = now,
                AtualizadoEmUtc = now,
                UltimoLoginEmUtc = now
            };
            _db.Usuarios.Add(user);
        }
        else
        {
            user.Email = googleUser.Email;
            user.Nome = googleUser.Name ?? googleUser.Email;
            user.FotoUrl = googleUser.Picture;
            user.UltimoLoginEmUtc = now;
            user.AtualizadoEmUtc = now;
        }

        await _bootstrap.ApplyFirstAdministratorAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(user.Id, cancellationToken)
            ?? throw new InvalidOperationException("O usuário autenticado não foi localizado após o login.");
    }

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Usuarios
            .AsNoTracking()
            .Include(x => x.GrupoUsuario!)
                .ThenInclude(x => x.GrupoUsuarioModulos)
                    .ThenInclude(x => x.Modulo)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public IQueryable<Usuario> QueryUsers() =>
        _db.Usuarios
            .AsNoTracking()
            .Include(x => x.GrupoUsuario);
}
