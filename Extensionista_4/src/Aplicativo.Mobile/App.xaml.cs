using Microsoft.Extensions.DependencyInjection;

namespace Aplicativo.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var authService = IPlatformApplication.Current?.Services.GetService<GoogleAuthService>()
                ?? new GoogleAuthService();
            return new Window(new AppShell(authService));
        }
    }
}
