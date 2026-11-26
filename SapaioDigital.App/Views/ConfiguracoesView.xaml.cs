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
            : $"Último backup ({cfg.Plataforma}): {cfg.UltimaDataBackup}";

        var status = BackupService.ObterStatusProvedores();
        TxtStatusPastasPC.Text = string.Join(" • ", status.Select(s => $"{s.Nome}: {(s.Disponivel ? "Detectado" : "Não encontrado")}"));

        var conta = CloudOAuthService.CarregarConta();
        if (conta != null && conta.Conectado)
        {
            TxtStatusContaOAuth.Text = $"Conta conectada via Navegador: {conta.Provedor}";
            TxtOAuthClientId.Text = conta.ClientId;
            BtnDesconectarOAuth.Visibility = Visibility.Visible;
        }
        else
        {
            TxtStatusContaOAuth.Text = "Nenhuma conta conectada via navegador";
            BtnDesconectarOAuth.Visibility = Visibility.Collapsed;
        }

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
            Cnpj = TxtCnpj.Text.Trim(),
            LogoPath = _logoPath,
            Configurada = true
        });
        MessageBox.Show("Dados da loja atualizados com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnObterClientId_Click(object sender, RoutedEventArgs e)
    {
        string provedor = (CmbProvedorOAuth.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Dropbox";
        Clipboard.SetText("http://127.0.0.1:54821/callback");

        string guia = provedor switch
        {
            "Dropbox" =>
                "PASSO A PASSO DROPBOX (Mais fácil - leva 1 minuto):
" +
                "1. Faça login no site que acabou de abrir (dropbox.com/developers/apps/create).
" +
                "2. Escolha 'Scoped access' -> 'App folder' -> dê o nome 'SampaioDigitalBackup' -> Create app.
" +
                "3. Na aba 'Permissions', marque 'files.content.write' e 'files.content.read' e clique em Submit.
" +
                "4. Na aba 'Settings', em 'Redirect URIs', cole (Ctrl+V) o endereço já copiado:
" +
                "   http://127.0.0.1:54821/callback e clique em Add.
" +
                "5. Copie o 'App key' (esse é o Client ID) e cole aqui no sistema!",
            "GoogleDrive" =>
                "PASSO A PASSO GOOGLE DRIVE:
" +
                "1. No Google Cloud Console aberto, crie um Projeto -> vá em 'APIs e Serviços' -> 'Tela de permissão OAuth' (Externo).
" +
                "2. Ative a 'Google Drive API' na Biblioteca.
" +
                "3. Em 'Credenciais' -> 'Criar Credenciais' -> 'ID do cliente OAuth' -> Tipo: 'Aplicativo para computador (Desktop)'.
" +
                "4. Copie o 'ID do cliente' e a 'Chave secreta' (Client Secret) e cole aqui!",
            _ =>
                "PASSO A PASSO ONEDRIVE (Microsoft Entra / Azure):
" +
                "1. No portal aberto, clique em 'Novo registro' -> Nome: 'SampaioDigital'.
" +
                "2. Em 'Tipos de conta com suporte', selecione 'Contas pessoais da Microsoft'.
" +
                "3. Em 'URI de Redirecionamento', escolha 'Aplicativo de cliente público/nativo' e cole (Ctrl+V):
" +
                "   http://127.0.0.1:54821/callback
" +
                "4. Copie o 'ID do aplicativo (cliente)' e cole aqui no sistema!"
        };

        string portalUrl = provedor switch
        {
            "Dropbox" => "https://www.dropbox.com/developers/apps/create",
            "OneDrive" => "https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade",
            "GoogleDrive" => "https://console.cloud.google.com/apis/credentials",
            _ => "https://www.dropbox.com/developers/apps/create"
        };

        Process.Start(new ProcessStartInfo(portalUrl) { UseShellExecute = true });
        MessageBox.Show(guia, $"Como gerar o Client ID ({provedor})", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BtnConectarOAuth_Click(object sender, RoutedEventArgs e)
    {
        string provedor = (CmbProvedorOAuth.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Dropbox";
        if (string.IsNullOrWhiteSpace(TxtOAuthClientId.Text))
        {
            var resp = MessageBox.Show(
                $"Para abrir a tela de login do {provedor} no navegador, é necessário informar o Client ID do aplicativo.\n\n" +
                $"Deseja abrir agora a página oficial para gerar seu Client ID em 1 minuto?",
                "Client ID necessário", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (resp == MessageBoxResult.Yes)
                BtnObterClientId_Click(sender, e);
            return;
        }

        BtnConectarOAuth.IsEnabled = false;
        TxtStatusContaOAuth.Text = "Aguardando autorização no navegador...";
        try
        {
            var conta = await CloudOAuthService.ConectarViaNavegadorAsync(provedor, TxtOAuthClientId.Text, TxtOAuthClientSecret.Text);
            CarregarConfigBackup();
            MessageBox.Show($"Conta {conta.Provedor} conectada com sucesso! Os backups serão enviados automaticamente para a nuvem.", "Nuvem Conectada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível conectar: {ex.Message}", "Erro OAuth", MessageBoxButton.OK, MessageBoxImage.Error);
            CarregarConfigBackup();
        }
        finally
        {
            BtnConectarOAuth.IsEnabled = true;
        }
    }

    private void BtnDesconectarOAuth_Click(object sender, RoutedEventArgs e)
    {
        CloudOAuthService.DesconectarConta();
        CarregarConfigBackup();
        MessageBox.Show("Conta da nuvem desconectada.", "Backup em Nuvem", MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (dir == null) { MessageBox.Show("OneDrive não detectado neste PC.", "OneDrive", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        DefinirDestinoBackup(dir, "OneDrive");
    }

    private void BtnDropbox_Click(object sender, RoutedEventArgs e)
    {
        var dir = BackupService.DetectarPastaDropbox();
        if (dir == null) { MessageBox.Show("Dropbox não detectado neste PC.", "Dropbox", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        DefinirDestinoBackup(dir, "Dropbox");
    }

    private void BtnGoogleDrive_Click(object sender, RoutedEventArgs e)
    {
        var dir = BackupService.DetectarPastaGoogleDrive();
        if (dir == null) { MessageBox.Show("Google Drive para Desktop não detectado neste PC.", "Google Drive", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        DefinirDestinoBackup(dir, "Google Drive");
    }

    private void BtnEscolherPasta_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Selecione a pasta de destino para os backups" };
        if (dlg.ShowDialog() == true)
            DefinirDestinoBackup(dlg.FolderName, "Pasta Local");
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
