from pathlib import Path
import subprocess,hashlib,json
root=Path.cwd(); backup='13afa372764a37b0877bd56cfe6082c1ab9e4d15'
def read(rel): return subprocess.check_output(['git','show',backup+':Extensionista_4/'+rel]).decode('utf-8-sig')
protected=list((root/'src/Aplicativo.Api').rglob('*.cs'))+list((root/'src/Aplicativo.Api').glob('*.json'))+[root/'src/Aplicativo.Mobile/GoogleAuthService.cs',root/'src/Aplicativo.Mobile/MainPage.xaml',root/'src/Aplicativo.Mobile/MainPage.xaml.cs']
protected += list((root/'src/Aplicativo.Mobile/Platforms').rglob('*'))
protected=[p for p in protected if p.is_file() and 'obj' not in p.parts and 'bin' not in p.parts]
(root/'tmp').mkdir(exist_ok=True)
(root/'tmp/auth-preservation.json').write_text(json.dumps({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}))
files=['src/Aplicativo.Core/Models/FleetData.cs','src/Aplicativo.Core/Validation/TripValidation.cs','src/Aplicativo.Mobile/Aplicativo.Mobile.csproj']
for folder in ['src/Aplicativo.Mobile/Data/Local','src/Aplicativo.Mobile/Services','src/Aplicativo.Mobile/Views','tests/Aplicativo.DemoChecks']:
    files += subprocess.check_output(['git','ls-tree','-r','--name-only',backup,'--',folder]).decode().splitlines()
files += ['src/Aplicativo.Mobile/Resources/Images/fleet_illustration.svg','src/Aplicativo.Mobile/Resources/Styles/Colors.xaml']
for rel in files:
    p=root/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(read(rel),encoding='utf-8')
# Keep MainPage and its authentication handlers byte-for-byte unchanged.
for suffix in ['.xaml','.xaml.cs','.Access.cs','.Dashboard.cs','.Finance.cs','.Logistics.cs']:
    s=read('src/Aplicativo.Mobile/MainPage'+suffix).replace('MainPage','FleetPage').replace('using Aplicativo.Core.Contracts;\n','')
    if suffix=='.xaml.cs':
        s=s.replace('    private AppSession? session;', '    private AppSession? session = null;')
        a=s.index('        try { session = await authService.GetStoredSessionAsync(); }');b=s.index('        if (store.LoadWarning',a)
        s=s[:a]+'        Login();\n'+s[b:]
        a=s.index('    private async Task SignInAsync()');b=s.index('    private void Login()',a)
        s=s[:a]+'''    private async Task SignInAsync()
    {
        if (signingIn) return;
        signingIn = true;
        try
        {
            // Delegate authentication to the existing, unchanged login page.
            var login = new MainPage(authService);
            var close = new ToolbarItem { Text = "Voltar" };
            close.Clicked += async (_, _) => await Navigation.PopModalAsync();
            login.ToolbarItems.Add(close);
            await Navigation.PushModalAsync(new NavigationPage(login));
        }
        finally { signingIn = false; }
    }

    private Task SignOutAsync()
    {
        // Only leaves the local demonstration. Real sign-out remains on MainPage.
        session = null;
        tab = 0;
        Login();
        return Task.CompletedTask;
    }
'''+s[b:]
    (root/('src/Aplicativo.Mobile/FleetPage'+suffix)).write_text(s,encoding='utf-8')
p=root/'src/Aplicativo.Mobile/AppShell.xaml.cs';s=p.read_text(encoding='utf-8-sig');s=s.replace('new MainPage(authService)','new FleetPage(authService, new Aplicativo.Mobile.Services.DemoStore())');p.write_text(s,encoding='utf-8')
p=root/'src/Aplicativo.Mobile/MauiProgram.cs';s=p.read_text(encoding='utf-8-sig').replace('builder.Services.AddSingleton<LocalDatabase>();','builder.Services.AddSingleton(_ => new LocalDatabase(Path.Combine(FileSystem.AppDataDirectory, "logtrack.db3")));');p.write_text(s,encoding='utf-8')
print('Telas recuperadas em FleetPage; pagina e servicos de login mantidos.')
