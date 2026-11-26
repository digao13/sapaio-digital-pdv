using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;
using SapaioDigital.App.Services;

namespace SapaioDigital.App.Views;

public partial class OrcamentosView : UserControl
{
    private Orcamento? _osEditando;

    public OrcamentosView()
    {
        InitializeComponent();
        InputValidator.AplicarSomenteDecimal(TxtValor);
        CarregarLoja();
        CarregarClientes();
        CarregarOrcamentos();
        Limpar();
    }

    private void CarregarLoja()
    {
        var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig();
        if (loja == null) return;
        TxtLojaNome.Text = loja.RazaoSocial;
        TxtLojaTel.Text = $"Tel / WhatsApp: {loja.Telefone}";
        if (!string.IsNullOrWhiteSpace(loja.LogoPath) && File.Exists(loja.LogoPath))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(loja.LogoPath);
            bmp.EndInit();
            ImgLojaLogo.Source = bmp;
        }
    }

    private void CarregarClientes()
    {
        CmbCliente.ItemsSource = DatabaseService.ListarClientes();
    }

    private void CarregarOrcamentos()
    {
        GridOrcamentos.ItemsSource = DatabaseService.ListarOrcamentos(null, null, TxtBusca.Text);
    }

    private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => CarregarOrcamentos();

    private void GridOrcamentos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridOrcamentos.SelectedItem is not Orcamento os) return;
        _osEditando = os;
        TxtTituloForm.Text = $"Editando OS #{os.Id:D5}";
        CmbCliente.SelectedValue = os.ClienteId;
        TxtEquipamento.Text = os.Equipamento;
        TxtProblema.Text = os.ProblemaRelatado;
        TxtServicos.Text = os.ServicosObservacoes;
        TxtValor.Text = os.ValorEstimado.ToString("N2");
        DpData.SelectedDate = os.DataHora.Date;
        TxtResponsavel.Text = os.UsuarioNome;

        foreach (ComboBoxItem item in CmbStatus.Items)
        {
            if (item.Content?.ToString() == os.Status)
            {
                CmbStatus.SelectedItem = item;
                break;
            }
        }
    }

    private void BtnLimpar_Click(object sender, RoutedEventArgs e) => Limpar();

    private void Limpar()
    {
        _osEditando = null;
        GridOrcamentos.SelectedItem = null;
        TxtTituloForm.Text = "Novo Orçamento / Ordem de Serviço";
        CmbCliente.SelectedIndex = -1;
        TxtEquipamento.Clear();
        TxtProblema.Clear();
        TxtServicos.Clear();
        TxtValor.Text = "0,00";
        DpData.SelectedDate = DateTime.Today;
        CmbStatus.SelectedIndex = 0;
        TxtResponsavel.Text = SessaoAtual.UsuarioLogado?.Nome ?? "Operador";
    }

    private Orcamento? SalvarAtual()
    {
        if (CmbCliente.SelectedItem is not Cliente cliente)
        {
            MessageBox.Show("Selecione um cliente.", "Campo obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
        if (string.IsNullOrWhiteSpace(TxtProblema.Text))
        {
            MessageBox.Show("Descreva o problema relatado pelo cliente.", "Campo obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        decimal.TryParse(TxtValor.Text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal valor);
        DateTime dataBase = DpData.SelectedDate ?? DateTime.Today;
        DateTime dataHora = _osEditando != null
            ? dataBase.Date.Add(_osEditando.DataHora.TimeOfDay)
            : dataBase.Date.Add(DateTime.Now.TimeOfDay);

        var usuario = SessaoAtual.UsuarioLogado;
        var os = _osEditando ?? new Orcamento();
        os.ClienteId = cliente.Id;
        os.ClienteNome = cliente.Nome;
        os.ClienteTelefone = cliente.TelefoneWhatsApp;
        os.ClienteEndereco = cliente.Endereco;
        os.Equipamento = TxtEquipamento.Text.Trim();
        os.ProblemaRelatado = TxtProblema.Text.Trim();
        os.ServicosObservacoes = TxtServicos.Text.Trim();
        os.ValorEstimado = valor;
        os.DataHora = dataHora;
        os.Status = (CmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Aberto";

        if (os.Id == 0)
        {
            os.UsuarioId = usuario?.Id;
            os.UsuarioNome = usuario?.Nome ?? "Operador";
        }

        DatabaseService.SalvarOrcamento(os);
        CarregarOrcamentos();
        return os;
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var os = SalvarAtual();
            if (os == null) return;
            Limpar();
            MessageBox.Show($"Ordem de Serviço #{os.Id:D5} salva com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar OS: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSalvarImprimir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var os = SalvarAtual();
            if (os == null) return;
            var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sapaio Digital" };
            PdfOsService.GerarOrdemServicoPdf(os, loja, abrirAposGerar: true);
            Limpar();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar PDF da OS: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImprimirSelecionada_Click(object sender, RoutedEventArgs e)
    {
        if (GridOrcamentos.SelectedItem is not Orcamento os)
        {
            MessageBox.Show("Selecione uma Ordem de Serviço na tabela.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sapaio Digital" };
            PdfOsService.GerarOrdemServicoPdf(os, loja, abrirAposGerar: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar PDF: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
