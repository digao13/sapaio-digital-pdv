using System;
using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<OrcamentoItem> _itensOs = new();

    public OrcamentosView()
    {
        InitializeComponent();
        InputValidator.AplicarSomenteDecimal(TxtValorServico);
        InputValidator.AplicarSomenteDecimal(TxtValor);
        InputValidator.AplicarSomenteInteiro(TxtQtdPeca);
        GridItensOs.ItemsSource = _itensOs;

        CarregarLoja();
        CarregarClientes();
        CarregarProdutos();
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

    private void CarregarClientes() => CmbCliente.ItemsSource = DatabaseService.ListarClientes();
    private void CarregarProdutos() => CmbProdutoEstoque.ItemsSource = DatabaseService.ListarProdutos();
    private void CarregarOrcamentos() => GridOrcamentos.ItemsSource = DatabaseService.ListarOrcamentos(null, null, TxtBusca.Text);

    private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => CarregarOrcamentos();

    private void BtnAddPeca_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProdutoEstoque.SelectedItem is not Produto p)
        {
            MessageBox.Show("Selecione um produto do estoque.", "Peças", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(TxtQtdPeca.Text, out int qtd) || qtd <= 0)
        {
            MessageBox.Show("Informe uma quantidade válida maior que zero.", "Peças", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int jaNaLista = _itensOs.Where(x => x.ProdutoId == p.Id && !x.Baixado).Sum(x => x.Quantidade);
        if (jaNaLista + qtd > p.Quantidade)
        {
            MessageBox.Show($"Estoque insuficiente para '{p.Nome}'. Disponível: {p.Quantidade}.", "Estoque", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _itensOs.Add(new OrcamentoItem
        {
            ProdutoId = p.Id,
            ProdutoNome = p.Nome,
            Quantidade = qtd,
            PrecoUnitario = p.PrecoVenda,
            Baixado = false
        });
        TxtQtdPeca.Text = "1";
        AtualizarTotaisOs();
    }

    private void BtnRemoverPeca_Click(object sender, RoutedEventArgs e)
    {
        if (GridItensOs.SelectedItem is not OrcamentoItem item) return;
        if (item.Baixado)
        {
            MessageBox.Show("Esta peça já foi baixada do estoque ao salvar a OS. Para devolver ao estoque, registre uma Entrada na tela de Produtos.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _itensOs.Remove(item);
        AtualizarTotaisOs();
    }

    private void TxtValorServico_TextChanged(object sender, TextChangedEventArgs e) => AtualizarTotaisOs();

    private void AtualizarTotaisOs()
    {
        if (TxtValorPecas == null) return;
        decimal pecas = _itensOs.Sum(x => x.Subtotal);
        InputValidator.TryParseDecimalPtBr(TxtValorServico.Text, out decimal servico);
        TxtValorPecas.Text = pecas.ToString("N2");
        TxtValor.Text = (servico + pecas).ToString("N2");

        var vis = _itensOs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        GridItensOs.Visibility = vis;
        BtnRemoverPeca.Visibility = vis;
    }

    private void GridOrcamentos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridOrcamentos.SelectedItem is not Orcamento os) return;
        _osEditando = os;
        TxtTituloForm.Text = $"Editando OS #{os.Id:D5}";
        CmbCliente.SelectedValue = os.ClienteId;
        TxtEquipamento.Text = os.Equipamento;
        TxtProblema.Text = os.ProblemaRelatado;
        TxtServicos.Text = os.ServicosObservacoes;
        DpData.SelectedDate = os.DataHora.Date;
        TxtResponsavel.Text = os.UsuarioNome;

        _itensOs.Clear();
        foreach (var it in os.Itens) _itensOs.Add(it);

        decimal pecas = _itensOs.Sum(x => x.Subtotal);
        decimal servico = os.ValorServico > 0 ? os.ValorServico : Math.Max(0, os.ValorEstimado - pecas);
        TxtValorServico.Text = servico.ToString("N2");
        AtualizarTotaisOs();
        TxtValor.Text = os.ValorEstimado.ToString("N2");

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
        _itensOs.Clear();
        TxtValorServico.Text = "0,00";
        AtualizarTotaisOs();
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

        InputValidator.TryParseDecimalPtBr(TxtValorServico.Text, out decimal valorServico);
        InputValidator.TryParseDecimalPtBr(TxtValor.Text, out decimal valorTotal);

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
        os.ValorServico = valorServico;
        os.ValorEstimado = valorTotal;
        os.DataHora = dataHora;
        os.Status = (CmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Aberto";
        os.Itens = _itensOs.ToList();

        if (os.Id == 0)
        {
            os.UsuarioId = usuario?.Id;
            os.UsuarioNome = usuario?.Nome ?? "Operador";
        }

        DatabaseService.SalvarOrcamento(os);
        CarregarProdutos();
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
            var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sampaio Digital" };
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
            var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sampaio Digital" };
            PdfOsService.GerarOrdemServicoPdf(os, loja, abrirAposGerar: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar PDF: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
