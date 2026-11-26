using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Views;

public partial class OnboardingWindow : Window
{
    private string _logoPath = string.Empty;
    private readonly bool _precisaCriarAdmin;

    public OnboardingWindow()
    {
        InitializeComponent();
        InputValidator.AplicarSomenteTelefone(TxtTelefone);
        _precisaCriarAdmin = !DatabaseService.ExisteAlgumUsuario();
        PanelAdmin.Visibility = _precisaCriarAdmin ? Visibility.Visible : Visibility.Collapsed;

        var loja = DatabaseService.ObterLojaConfig();
        if (loja != null)
        {
            TxtRazaoSocial.Text = loja.RazaoSocial;
            TxtTelefone.Text = loja.Telefone;
            TxtCnpj.Text = loja.Cnpj;
            if (!string.IsNullOrWhiteSpace(loja.LogoPath) && File.Exists(loja.LogoPath))
            {
                _logoPath = loja.LogoPath;
                CarregarPreview(_logoPath);
            }
        }
    }

    private void BtnSelecionarLogo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Selecione a Logo da Loja",
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dlg.ShowDialog() == true)
        {
            _logoPath = dlg.FileName;
            CarregarPreview(_logoPath);
        }
    }

    private void CarregarPreview(string path)
    {
        TxtLogoArquivo.Text = Path.GetFileName(path);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        ImgLogoPreview.Source = bmp;
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtRazaoSocial.Text))
        {
            MessageBox.Show("Informe a Razão Social da loja.", "Campo obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!InputValidator.TelefoneValido(TxtTelefone.Text))
        {
            MessageBox.Show("Informe um Telefone válido da loja (mínimo 8 dígitos).", "Campo obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_logoPath) || !File.Exists(_logoPath))
        {
            MessageBox.Show("Selecione uma foto/logo para a loja.", "Campo obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_precisaCriarAdmin)
        {
            if (string.IsNullOrWhiteSpace(TxtAdminNome.Text) || string.IsNullOrWhiteSpace(TxtAdminLogin.Text) || string.IsNullOrWhiteSpace(TxtAdminSenha.Password))
            {
                MessageBox.Show("Preencha Nome, Login e Senha do usuário administrador inicial.", "Campo obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        try
        {
            var config = new LojaConfig
            {
                RazaoSocial = TxtRazaoSocial.Text.Trim(),
                Telefone = TxtTelefone.Text.Trim(),
                Cnpj = TxtCnpj.Text.Trim(),
                LogoPath = _logoPath,
                Configurada = true
            };
            DatabaseService.SalvarLojaConfig(config);

            if (_precisaCriarAdmin)
            {
                DatabaseService.SalvarUsuario(new Usuario
                {
                    Nome = TxtAdminNome.Text.Trim(),
                    Login = TxtAdminLogin.Text.Trim(),
                    Perfil = "Administrador",
                    Ativo = true
                }, TxtAdminSenha.Password);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar configuração inicial: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
