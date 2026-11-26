using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Services;
using SapaioDigital.App.Views;

namespace SapaioDigital.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private bool _sidebarRecolhida;

    public MainWindow()
    {
        InitializeComponent();
        AtualizarCabecalhoLoja();
        AtualizarUsuario();
        AtualizarBotaoTema();
        TxtVersaoApp.Text = $"Versão {GitHubUpdaterService.ObterVersaoAtual()}";
        SessaoAtual.LojaAtualizada += AtualizarCabecalhoLoja;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => TxtDataHoraTopo.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss");
        _timer.Start();
        TxtDataHoraTopo.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss");

        NavegarParaPdv();
        Loaded += async (_, _) => await VerificarAtualizacaoSilenciosaAsync();
    }

    private async System.Threading.Tasks.Task VerificarAtualizacaoSilenciosaAsync()
    {
        try
        {
            var info = await GitHubUpdaterService.VerificarAtualizacaoAsync();
            if (info.Disponivel)
            {
                BtnUpdateDisponivel.Content = $"Atualizar para v{info.NovaVersao}";
                BtnUpdateDisponivel.Visibility = Visibility.Visible;
            }
        }
        catch { }
    }

    private void AtualizarBotaoTema()
    {
        BtnToggleTema.Content = ThemeService.IsDark ? "Tema Claro" : "Tema Escuro";
    }

    private void BtnToggleTema_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.ToggleTheme();
        AtualizarBotaoTema();
    }

    private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _sidebarRecolhida = !_sidebarRecolhida;
        var vis = _sidebarRecolhida ? Visibility.Collapsed : Visibility.Visible;
        ColSidebar.Width = new GridLength(_sidebarRecolhida ? 68 : 252);
        PanelSidebarHeader.Visibility = vis;
        PanelSidebarFooter.Visibility = vis;

        foreach (var lbl in new[] { LblNavPdv, LblNavOrc, LblNavProd, LblNavCli, LblNavRel, LblNavUsu, LblNavCfg, LblNavUpd })
            lbl.Visibility = vis;

        var align = _sidebarRecolhida ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        foreach (var b in new[] { BtnNavPdv, BtnNavOrc, BtnNavProd, BtnNavCli, BtnNavRel, BtnNavUsu, BtnNavCfg, BtnNavUpd })
            b.HorizontalContentAlignment = align;
    }

    private void AtualizarCabecalhoLoja()
    {
        var loja = SessaoAtual.Loja;
        if (loja == null) return;

        TxtSidebarLoja.Text = loja.RazaoSocial;
        TxtSidebarTelefone.Text = $"Tel: {loja.Telefone}";

        if (!string.IsNullOrWhiteSpace(loja.LogoPath) && File.Exists(loja.LogoPath))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(loja.LogoPath);
            bmp.EndInit();
            ImgSidebarLogo.Source = bmp;
        }
    }

    private void AtualizarUsuario()
    {
        var u = SessaoAtual.UsuarioLogado;
        TxtUsuarioConectado.Text = u != null ? $"{u.Nome} ({u.Perfil})" : "Desconectado";
    }

    private void NavegarParaPdv()
    {
        TxtTituloPagina.Text = "PDV — Ponto de Venda";
        MainContent.Content = new PdvView();
    }

    private void NavPdv_Click(object sender, RoutedEventArgs e) => NavegarParaPdv();

    private void NavOrcamentos_Click(object sender, RoutedEventArgs e)
    {
        TxtTituloPagina.Text = "Orçamentos e Ordens de Serviço";
        MainContent.Content = new OrcamentosView();
    }

    private void NavProdutos_Click(object sender, RoutedEventArgs e)
    {
        TxtTituloPagina.Text = "Produtos e Movimentação de Estoque";
        MainContent.Content = new ProdutosView();
    }

    private void NavClientes_Click(object sender, RoutedEventArgs e)
    {
        TxtTituloPagina.Text = "Clientes";
        MainContent.Content = new ClientesView();
    }

    private void NavRelatorios_Click(object sender, RoutedEventArgs e)
    {
        TxtTituloPagina.Text = "Relatórios e Dashboard";
        MainContent.Content = new RelatoriosView();
    }

    private void NavUsuarios_Click(object sender, RoutedEventArgs e)
    {
        TxtTituloPagina.Text = "Usuários";
        MainContent.Content = new UsuariosView();
    }

    private void NavConfiguracoes_Click(object sender, RoutedEventArgs e)
    {
        TxtTituloPagina.Text = "Dados da Loja, Backup e Atualização";
        MainContent.Content = new ConfiguracoesView();
    }

    private void BtnTrocarUsuario_Click(object sender, RoutedEventArgs e)
    {
        var login = new LoginWindow { Owner = this };
        if (login.ShowDialog() == true)
        {
            AtualizarUsuario();
            NavegarParaPdv();
        }
    }
}
