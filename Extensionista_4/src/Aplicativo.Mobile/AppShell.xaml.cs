namespace Aplicativo.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell(GoogleAuthService authService)
        {
            InitializeComponent();
            MainPageContent.Content = new MainPage(authService);
        }
    }
}
