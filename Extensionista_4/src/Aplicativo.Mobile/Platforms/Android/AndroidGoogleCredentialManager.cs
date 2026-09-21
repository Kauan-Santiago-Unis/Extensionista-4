using Android.OS;
using AndroidX.Core.Content;
using AndroidX.Credentials;
using Google.Android.Libraries.Identity.GoogleId;
using Microsoft.Maui.ApplicationModel;

namespace Aplicativo.Mobile;

internal static class AndroidGoogleCredentialManager
{
    public static async Task<GoogleIdTokenResult> GetIdTokenAsync(string serverClientId, CancellationToken cancellationToken)
    {
        var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("Nenhuma Activity Android está ativa.");
        var nonce = CreateNonce();
        var manager = CredentialManager.Create(activity);
        // Este método é acionado pelo botão "Continuar com Google".
        // GetSignInWithGoogleOption é o fluxo de botão do Credential Manager
        // e também funciona quando a conta ainda precisa autorizar o app ou
        // exige uma nova autenticação.
        var option = new GetSignInWithGoogleOption.Builder(serverClientId)
            .SetNonce(nonce)
            .Build();
        var request = new GetCredentialRequest.Builder().AddCredentialOption(option).Build();
        var callback = new CredentialCallback();
        using var registration = cancellationToken.Register(callback.Cancel);
        manager.GetCredentialAsync(activity, request, null, ContextCompat.GetMainExecutor(activity)!, callback);
        var response = await callback.Task.ConfigureAwait(false);
        if (response.Credential is not CustomCredential custom || custom.Type != GoogleIdTokenCredential.TypeGoogleIdTokenCredential)
            throw new InvalidOperationException("O Credential Manager não retornou uma credencial Google.");
        var googleCredential = GoogleIdTokenCredential.CreateFrom(custom.Data);
        return new GoogleIdTokenResult(googleCredential.IdToken, nonce);
    }

    private static string CreateNonce()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class CredentialCallback : Java.Lang.Object, ICredentialManagerCallback
    {
        private readonly TaskCompletionSource<GetCredentialResponse> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<GetCredentialResponse> Task => _completion.Task;
        public void OnError(Java.Lang.Object error) => _completion.TrySetException(new InvalidOperationException($"Falha no Credential Manager: {error}"));
        public void OnResult(Java.Lang.Object? result)
        {
            if (result is GetCredentialResponse response) _completion.TrySetResult(response);
            else _completion.TrySetException(new InvalidOperationException("Resposta inválida do Credential Manager."));
        }
        public void Cancel() => _completion.TrySetCanceled();
    }
}
