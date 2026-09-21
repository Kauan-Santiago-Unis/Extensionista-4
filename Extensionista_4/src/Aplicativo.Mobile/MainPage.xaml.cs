namespace Aplicativo.Mobile;

public partial class MainPage : ContentPage
{
    private readonly GoogleAuthService _authService;

    public MainPage(GoogleAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _ = RestoreSessionAsync();
    }

    private async Task RestoreSessionAsync()
    {
        var session = await _authService.GetStoredSessionAsync();
        if (session is null) return;
        StatusLabel.Text = $"Sessão ativa: {session.Email}";
        GoogleLoginButton.IsVisible = false;
        LogoutButton.IsVisible = true;
    }

    private async void OnGoogleLoginClicked(object? sender, EventArgs e)
    {
        try
        {
            GoogleLoginButton.IsEnabled = false;
            StatusLabel.Text = "Abrindo o Google...";
            var session = await _authService.SignInAsync();
            StatusLabel.Text = $"Olá, {session.Name}!";
            GoogleLoginButton.IsVisible = false;
            LogoutButton.IsVisible = true;
        }
        catch (TaskCanceledException) { StatusLabel.Text = "Login cancelado."; }
        catch (Exception ex) { StatusLabel.Text = $"Falha no login: {ex.Message}"; }
        finally { GoogleLoginButton.IsEnabled = true; }
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await _authService.SignOutAsync();
        StatusLabel.Text = "Sessão encerrada.";
        GoogleLoginButton.IsVisible = true;
        LogoutButton.IsVisible = false;
    }
}
