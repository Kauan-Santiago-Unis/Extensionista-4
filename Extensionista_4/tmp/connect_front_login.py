from pathlib import Path
p=Path('src/Aplicativo.Mobile/FleetPage.xaml.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('        Login();\n        if (store.LoadWarning', '''        try { session = await authService.GetStoredSessionAsync(); }
        catch (Exception) { await DisplayAlertAsync("Sessão", "Não foi possível restaurar sua sessão. Entre novamente.", "Entendi"); }
        if (session is null) Login(); else PendingApproval();
        if (store.LoadWarning''',1)
a=s.index('    private async Task SignInAsync()');b=s.index('    private void Login()',a)
s=s[:a]+'''    private async Task SignInAsync()
    {
        if (signingIn) return;
        signingIn = true;
        Root.IsEnabled = false;
        try
        {
            session = await authService.SignInAsync();
            PendingApproval();
        }
        catch (OperationCanceledException)
        { await DisplayAlertAsync("Login cancelado", "Você pode tentar novamente quando quiser.", "Entendi"); }
        catch (HttpRequestException)
        { await DisplayAlertAsync("Falha no login", "Não foi possível conectar ao Google ou à API. Verifique sua conexão e se o perfil FullStack está em execução.", "Entendi"); }
        catch (Exception)
        { await DisplayAlertAsync("Falha no login", "Não foi possível concluir o login. Confira a configuração Google e a execução da API.", "Entendi"); }
        finally { signingIn = false; Root.IsEnabled = true; }
    }

    private async Task SignOutAsync()
    {
        if (signingIn) return;
        signingIn = true;
        Root.IsEnabled = false;
        try
        {
            await authService.SignOutAsync();
            session = null;
            tab = 0;
            Login();
        }
        catch (Exception)
        { await DisplayAlertAsync("Não foi possível sair", "Tente novamente para encerrar a sessão salva.", "Entendi"); }
        finally { signingIn = false; Root.IsEnabled = true; }
    }
'''+s[b:];p.write_text(s,encoding='utf-8')
