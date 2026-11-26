using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private bool _carregandoBackup;

    public ConfiguracoesView()
    {
        InitializeComponent();
        InputValidator.AplicarSomenteTelefone(TxtTelefone);

        var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig();
        if (loja != null)
        {
            TxtRazaoSocial.Text = loja.RazaoSocial;
            TxtTelefone.Text = loja.Telefone;
            _logoPath = loja.LogoPath;
            CarregarPreview(_logoPath);
        }

        TxtVersao.Text = $"Versão instalada: {GitHubUpdaterService.ObterVersaoAtual()}";
        CarregarConfigBackup();
    }

    private void CarregarConfigBackup()
    {
        _carregandoBackup = true;
        var cfg = BackupService.CarregarConfig();
        TxtPlataformaAtiva.Text = cfg.Plataforma;
        TxtPastaBackup.Text = cfg.PastaDestino;
        ChkBackupDiario.IsChecked = cfg.BackupDiarioAutomatico;
        TxtUltimoBackup.Text = string.IsNullOrWhiteSpace(cfg.UltimaDataBackup)
            ? "Último backup: ainda não realizado"
            : $"Último backup ({cfg.Plataforma}): {cfg.UltimaDataBackup}";

        var status = BackupService.ObterStatusProvedores();
        var one = status.First(s => s.Nome == "OneDrive");
        var gd = status.First(s => s.Nome == "Google Drive");
        var db = status.First(s => s.Nome == "Dropbox");

        TxtStatusOneDrive.Text = $"OneDrive: {(one.Disponivel ? "Conectado" : "Não encontrado")}";
        TxtStatusGoogleDrive.Text = $"Google Drive: {(gd.Disponivel ? "Conectado" : "Não encontrado")}";
        TxtStatusDropbox.Text = $"Dropbox: {(db.Disponivel ? "Conectado" : "Não encontrado")}";

        GridBackups.ItemsSource = BackupService.ListarBackupsDisponiveis();
        _carregandoBackup = false;
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
        if (string.IsNullOrWhiteSpace(TxtRazaoSocial.Text) || !InputValidator.TelefoneValido(TxtTelefone.Text) || string.IsNullOrWhiteSpace(_logoPath))
        {
            MessageBox.Show("Preencha Razão Social, Telefone válido (mín. 8 dígitos) e selecione uma Logo.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private void DefinirDestinoBackup(string pasta, string plataforma)
    {
        Directory.CreateDirectory(pasta);
        var cfg = BackupService.CarregarConfig();
        cfg.PastaDestino = pasta;
        cfg.Plataforma = plataforma;
        BackupService.SalvarConfig(cfg);
        CarregarConfigBackup();
        MessageBox.Show($"Destino de backup definido para {plataforma}:\n{pasta}", "Backup Configurado", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnOneDrive_Click(object sender, RoutedEventArgs e)
    {
        var dir = BackupService.DetectarPastaOneDrive();
        if (dir == null)
        {
            MessageBox.Show("OneDrive não detectado neste PC. Faça login no OneDrive do Windows ou use 'Escolher Pasta Local...'.", "OneDrive", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DefinirDestinoBackup(dir, "OneDrive");
    }

    private void BtnDropbox_Click(object sender, RoutedEventArgs e)
    {
        var dir = BackupService.DetectarPastaDropbox();
        if (dir == null)
        {
            MessageBox.Show("Dropbox não detectado neste PC. Instale o Dropbox ou use 'Escolher Pasta Local...'.", "Dropbox", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DefinirDestinoBackup(dir, "Dropbox");
    }

    private void BtnGoogleDrive_Click(object sender, RoutedEventArgs e)
    {
        var dir = BackupService.DetectarPastaGoogleDrive();
        if (dir == null)
        {
            MessageBox.Show("Google Drive não detectado. Instale o 'Google Drive para Desktop' no Windows ou selecione a pasta em 'Escolher Pasta Local...'.", "Google Drive", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DefinirDestinoBackup(dir, "Google Drive");
    }

    private void BtnEscolherPasta_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Selecione a pasta de destino para os backups (Local ou Nuvem)"
        };
        if (dlg.ShowDialog() == true)
        {
            DefinirDestinoBackup(dlg.FolderName, "Pasta Personalizada");
        }
    }

    private void ChkBackupDiario_Changed(object sender, RoutedEventArgs e)
    {
        if (_carregandoBackup) return;
        var cfg = BackupService.CarregarConfig();
        cfg.BackupDiarioAutomatico = ChkBackupDiario.IsChecked == true;
        BackupService.SalvarConfig(cfg);
    }

    private void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = BackupService.ExecutarBackup("manual");
            CarregarConfigBackup();
            MessageBox.Show($"Backup criado com sucesso em:\n{path}", "Backup SQLite", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao criar backup: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAbrirPastaBackup_Click(object sender, RoutedEventArgs e)
    {
        var pasta = BackupService.CarregarConfig().PastaDestino;
        Directory.CreateDirectory(pasta);
        Process.Start(new ProcessStartInfo(pasta) { UseShellExecute = true });
    }

    private void BtnRestaurarBackup_Click(object sender, RoutedEventArgs e)
    {
        if (GridBackups.SelectedItem is not BackupArquivoInfo item)
        {
            MessageBox.Show("Selecione um backup na lista para restaurar.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Deseja restaurar o banco de dados para o backup '{item.NomeArquivo}' ({item.DataCriacao:dd/MM/yyyy HH:mm})?\n\nUm backup de segurança do estado atual será criado antes da restauração.",
            "Confirmar Restauração", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            BackupService.RestaurarBackup(item.CaminhoCompleto);
            MessageBox.Show("Backup restaurado com sucesso! Recomenda-se reiniciar o aplicativo.", "Restauração Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao restaurar backup: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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
