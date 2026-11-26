using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;
using SapaioDigital.App.Services;

namespace SapaioDigital.App.Views;

public partial class ConfiguracoesView : UserControl
{
    private string _logoPath = "";
    private string _downloadUrl = "";

    public ConfiguracoesView()
    {
        InitializeComponent();
        var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig();
        if (loja != null)
        {
            TxtRazaoSocial.Text = loja.RazaoSocial;
            TxtTelefone.Text = loja.Telefone;
            _logoPath = loja.LogoPath;
            CarregarPreview(_logoPath);
        }

        TxtVersao.Text = $"Versão instalada: {GitHubUpdaterService.ObterVersaoAtual()}";
        TxtCaminhoBanco.Text = AppPaths.DatabasePath;
    }

    private void CarregarPreview(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        ImgLogo.Source = bmp;
    }

    private void BtnSelecionarLogo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Selecionar Logo da Loja",
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };
        if (dlg.ShowDialog() == true)
        {
            _logoPath = dlg.FileName;
            CarregarPreview(_logoPath);
        }
    }

    private void BtnSalvarLoja_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtRazaoSocial.Text) || string.IsNullOrWhiteSpace(TxtTelefone.Text) || string.IsNullOrWhiteSpace(_logoPath))
        {
            MessageBox.Show("Preencha Razão Social, Telefone e selecione uma Logo.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DatabaseService.SalvarLojaConfig(new LojaConfig
        {
            RazaoSocial = TxtRazaoSocial.Text.Trim(),
            Telefone = TxtTelefone.Text.Trim(),
            LogoPath = _logoPath,
            Configurada = true
        });
        MessageBox.Show("Dados da loja atualizados com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = DatabaseService.CriarBackupSeguro("manual");
            MessageBox.Show($"Backup criado com sucesso em:\n{path}", "Backup SQLite", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao criar backup: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnVerificar_Click(object sender, RoutedEventArgs e)
    {
        BtnVerificar.IsEnabled = false;
        TxtStatusUpdate.Text = "Consultando GitHub Releases...";
        try
        {
            var info = await GitHubUpdaterService.VerificarAtualizacaoAsync();
            if (info.Disponivel)
            {
                _downloadUrl = info.DownloadUrl;
                TxtStatusUpdate.Text = $"Nova versão disponível: {info.NovaVersao}!\n{info.Notas}";
                BtnAplicar.Visibility = Visibility.Visible;
            }
            else
            {
                TxtStatusUpdate.Text = $"Você já está na versão mais recente ({info.VersaoAtual}).";
                BtnAplicar.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            TxtStatusUpdate.Text = $"Erro ao consultar GitHub: {ex.Message}";
        }
        finally
        {
            BtnVerificar.IsEnabled = true;
        }
    }

    private async void BtnAplicar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_downloadUrl)) return;
        BtnAplicar.IsEnabled = false;
        TxtStatusUpdate.Text = "Fazendo backup do banco e baixando atualização...";
        try
        {
            await GitHubUpdaterService.BaixarEAplicarAtualizacaoAsync(_downloadUrl);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            TxtStatusUpdate.Text = $"Erro ao atualizar: {ex.Message}";
            BtnAplicar.IsEnabled = true;
        }
    }
}
