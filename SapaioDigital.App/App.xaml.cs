using System;
using System.Windows;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Views;

namespace SapaioDigital.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            DatabaseService.Initialize();
            ThemeService.LoadSavedTheme();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao inicializar o banco de dados SQLite:\n{ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var loja = DatabaseService.ObterLojaConfig();
        bool precisaOnboarding = loja == null || !loja.Configurada || !DatabaseService.ExisteAlgumUsuario();

        if (precisaOnboarding)
        {
            var onboarding = new OnboardingWindow();
            if (onboarding.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            loja = DatabaseService.ObterLojaConfig();
        }

        SessaoAtual.Loja = loja;

        var login = new LoginWindow();
        if (login.ShowDialog() != true || SessaoAtual.UsuarioLogado == null)
        {
            Shutdown();
            return;
        }

        var main = new MainWindow();
        MainWindow = main;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
    }
}
