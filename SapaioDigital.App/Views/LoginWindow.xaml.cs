using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;

namespace SapaioDigital.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig();
        if (loja != null)
        {
            TxtNomeLoja.Text = loja.RazaoSocial;
            if (!string.IsNullOrWhiteSpace(loja.LogoPath) && File.Exists(loja.LogoPath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(loja.LogoPath);
                bmp.EndInit();
                ImgLogo.Source = bmp;
            }
        }
        TxtLogin.Focus();
    }

    private void TxtSenha_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            TentarLogin();
    }

    private void BtnEntrar_Click(object sender, RoutedEventArgs e) => TentarLogin();

    private void TentarLogin()
    {
        var usuario = DatabaseService.Autenticar(TxtLogin.Text, TxtSenha.Password);
        if (usuario == null)
        {
            MessageBox.Show("Usuário ou senha inválidos.", "Acesso Negado", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtSenha.Clear();
            TxtSenha.Focus();
            return;
        }

        SessaoAtual.UsuarioLogado = usuario;
        DialogResult = true;
        Close();
    }
}
