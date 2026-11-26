using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            TxtCnpj.Text = loja.Cnpj;
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
        TxtPastaBackup.Text = cfg.PastaDestino;
        ChkBackupDiario.IsChecked = cfg.BackupDiarioAutomatico;
        TxtUltimoBackup.Text = string.IsNullOrWhiteSpace(cfg.UltimaDataBackup)
            ? "Último backup: ainda não realizado"
            : $"Último backup: {cfg.UltimaDataBackup}";

        var contas = CloudOAuthService.CarregarContas();
        AtualizarCardNuvem("Dropbox", contas, TxtStatusDropbox, TxtDropboxId, null, BtnDesconectarDropbox);
        AtualizarCardNuvem("OneDrive", contas, TxtStatusOneDrive, TxtOneDriveId, null, BtnDesconectarOneDrive);
        AtualizarCardNuvem("GoogleDrive", contas, TxtStatusGoogleDrive, TxtGoogleId, TxtGoogleSecret, BtnDesconectarGoogle);

        GridBackups.ItemsSource = BackupService.ListarBackupsDisponiveis();
        _carregandoBackup = false;
    }

    private static void AtualizarCardNuvem(
        string provedor,
        System.Collections.Generic.Dictionary<string, CloudOAuthAccount> contas,
        TextBlock statusTb,
        TextBox idTb,
        TextBox? secretTb,
        Button btnDesconectar)
    {
        if (contas.TryGetValue(provedor, out var conta) && conta.Conectado)
        {
            statusTb.Text = "Conectado";
            statusTb.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            if (string.IsNullOrWhiteSpace(idTb.Text)) idTb.Text = conta.ClientId;
            if (secretTb != null && string.IsNullOrWhiteSpace(secretTb.Text)) secretTb.Text = conta.ClientSecret;
            btnDesconectar.Visibility = Visibility.Visible;
        }
        else
        {
            statusTb.Text = "Desconectado";
            statusTb.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            btnDesconectar.Visibility = Visibility.Collapsed;
        }
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
            Cnpj = TxtCnpj.Text.Trim(),
            LogoPath = _logoPath,
            Configurada = true
        });
        MessageBox.Show("Dados da loja atualizados com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ConectarProvedorAsync(string provedor, string clientId, string clientSecret, Button btn)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            MessageBox.Show($"Informe o Client ID do {provedor} conforme o passo a passo exibido no card.", "Client ID obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        btn.IsEnabled = false;
        try
        {
            await CloudOAuthService.ConectarViaNavegadorAsync(provedor, clientId, clientSecret);
            CarregarConfigBackup();
            MessageBox.Show($"{provedor} conectado com sucesso! Os backups serão enviados automaticamente para esta conta.", "Nuvem Conectada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível conectar ({provedor}): {ex.Message}", "Erro OAuth", MessageBoxButton.OK, MessageBoxImage.Error);
            CarregarConfigBackup();
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private void BtnConectarDropbox_Click(object sender, RoutedEventArgs e) => ConectarProvedorAsync("Dropbox", TxtDropboxId.Text, "", BtnConectarDropbox);
    private void BtnConectarOneDrive_Click(object sender, RoutedEventArgs e) => ConectarProvedorAsync("OneDrive", TxtOneDriveId.Text, "", BtnConectarOneDrive);
    private void BtnConectarGoogle_Click(object sender, RoutedEventArgs e) => ConectarProvedorAsync("GoogleDrive", TxtGoogleId.Text, TxtGoogleSecret.Text, BtnConectarGoogle);

    private void BtnDesconectarDropbox_Click(object sender, RoutedEventArgs e) { CloudOAuthService.DesconectarConta("Dropbox"); CarregarConfigBackup(); }
    private void BtnDesconectarOneDrive_Click(object sender, RoutedEventArgs e) { CloudOAuthService.DesconectarConta("OneDrive"); CarregarConfigBackup(); }
    private void BtnDesconectarGoogle_Click(object sender, RoutedEventArgs e) { CloudOAuthService.DesconectarConta("GoogleDrive"); CarregarConfigBackup(); }

    private void BtnPortalDropbox_Click(object sender, RoutedEventArgs e) { Clipboard.SetText("http://127.0.0.1:54821/callback"); Process.Start(new ProcessStartInfo("https://www.dropbox.com/developers/apps/create") { UseShellExecute = true }); }
    private void BtnPortalOneDrive_Click(object sender, RoutedEventArgs e) { Clipboard.SetText("http://127.0.0.1:54821/callback"); Process.Start(new ProcessStartInfo("https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade") { UseShellExecute = true }); }
    private void BtnPortalGoogle_Click(object sender, RoutedEventArgs e) { Clipboard.SetText("http://127.0.0.1:54821/callback"); Process.Start(new ProcessStartInfo("https://console.cloud.google.com/apis/credentials") { UseShellExecute = true }); }

    private void BtnEscolherPasta_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Selecione a pasta local para os backups" };
        if (dlg.ShowDialog() == true)
        {
            Directory.CreateDirectory(dlg.FolderName);
            var cfg = BackupService.CarregarConfig();
            cfg.PastaDestino = dlg.FolderName;
            BackupService.SalvarConfig(cfg);
            CarregarConfigBackup();
        }
    }

    private void ChkBackupDiario_Changed(object sender, RoutedEventArgs e)
    {
        if (_carregandoBackup) return;
        var cfg = BackupService.CarregarConfig();
        cfg.BackupDiarioAutomatico = ChkBackupDiario.IsChecked == true;
        BackupService.SalvarConfig(cfg);
    }

    private async void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = BackupService.ExecutarBackup("manual");
            var nuvens = await CloudOAuthService.EnviarParaTodasNuvensAsync(path);
            CarregarConfigBackup();
            string msgNuvem = nuvens.Count > 0 ? $"\nEnviado também para: {string.Join(", ", nuvens)}" : "\n(Nenhuma nuvem OAuth conectada no momento)";
            MessageBox.Show($"Backup local criado com sucesso em:\n{path}{msgNuvem}", "Backup Concluído", MessageBoxButton.OK, MessageBoxImage.Information);
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
            $"Deseja restaurar o banco de dados para '{item.NomeArquivo}' ({item.DataCriacao:dd/MM/yyyy HH:mm})?\n\nUm backup de segurança do estado atual será criado antes.",
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
