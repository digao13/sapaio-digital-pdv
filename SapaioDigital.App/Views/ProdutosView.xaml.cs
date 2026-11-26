using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Views;

public partial class ProdutosView : UserControl
{
    private Produto? _produtoEditando;
    private string _tipoMovAtual = "ENTRADA";

    public ProdutosView()
    {
        InitializeComponent();
        CarregarProdutos();
    }

    private void CarregarProdutos()
    {
        GridProdutos.ItemsSource = DatabaseService.ListarProdutos(TxtBusca.Text);
    }

    private void TxtBusca_TextChanged(object sender, TextChangedEventArgs e) => CarregarProdutos();

    private void GridProdutos_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridProdutos.SelectedItem is not Produto p) return;
        _produtoEditando = p;
        TxtTituloForm.Text = $"Editando Produto #{p.Id}";
        TxtNome.Text = p.Nome;
        TxtCodigoBarras.Text = p.CodigoBarras ?? "";
        TxtQuantidade.Text = p.Quantidade.ToString();
        TxtQuantidade.IsEnabled = false; // Estoque existente altera via Entrada/Saída
        TxtUnidade.Text = p.Unidade;
        TxtPrecoVenda.Text = p.PrecoVenda.ToString("N2");
        TxtPrecoCusto.Text = p.PrecoCusto.ToString("N2");
    }

    private void BtnLimpar_Click(object sender, RoutedEventArgs e) => LimparFormulario();

    private void LimparFormulario()
    {
        _produtoEditando = null;
        GridProdutos.SelectedItem = null;
        TxtTituloForm.Text = "Novo Produto";
        TxtNome.Clear();
        TxtCodigoBarras.Clear();
        TxtQuantidade.Text = "0";
        TxtQuantidade.IsEnabled = true;
        TxtUnidade.Text = "UN";
        TxtPrecoVenda.Text = "0,00";
        TxtPrecoCusto.Text = "0,00";
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNome.Text))
        {
            MessageBox.Show("Informe o nome do produto.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(TxtQuantidade.Text, out int qtd) || qtd < 0)
        {
            MessageBox.Show("Quantidade inválida.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!decimal.TryParse(TxtPrecoVenda.Text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal pv) || pv < 0)
        {
            MessageBox.Show("Preço de venda inválido.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        decimal.TryParse(TxtPrecoCusto.Text, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal pc);

        try
        {
            var prod = _produtoEditando ?? new Produto();
            prod.Nome = TxtNome.Text.Trim();
            prod.CodigoBarras = string.IsNullOrWhiteSpace(TxtCodigoBarras.Text) ? null : TxtCodigoBarras.Text.Trim();
            if (prod.Id == 0) prod.Quantidade = qtd;
            prod.Unidade = TxtUnidade.Text.Trim();
            prod.PrecoVenda = pv;
            prod.PrecoCusto = pc;

            var u = SessaoAtual.UsuarioLogado;
            DatabaseService.SalvarProduto(prod, u?.Id, u?.Nome ?? "Sistema");
            CarregarProdutos();
            LimparFormulario();
            MessageBox.Show("Produto salvo com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar produto: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnEntrada_Click(object sender, RoutedEventArgs e) => AbrirMovimentacao("ENTRADA");
    private void BtnSaida_Click(object sender, RoutedEventArgs e) => AbrirMovimentacao("SAIDA");

    private void AbrirMovimentacao(string tipo)
    {
        if (GridProdutos.SelectedItem is not Produto p)
        {
            MessageBox.Show("Selecione um produto na tabela primeiro.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _tipoMovAtual = tipo;
        TxtTituloMov.Text = tipo == "ENTRADA" ? "Registrar Entrada de Estoque" : "Registrar Saída de Estoque";
        TxtProdutoMov.Text = $"{p.Nome} (Estoque atual: {p.Quantidade} {p.Unidade})";
        TxtQtdMov.Text = "1";
        TxtMotivoMov.Text = tipo == "ENTRADA" ? "Reposição de estoque" : "Saída / Ajuste";
        PanelMovimentacao.Visibility = Visibility.Visible;
    }

    private void BtnCancelarMov_Click(object sender, RoutedEventArgs e)
    {
        PanelMovimentacao.Visibility = Visibility.Collapsed;
    }

    private void BtnConfirmarMov_Click(object sender, RoutedEventArgs e)
    {
        if (GridProdutos.SelectedItem is not Produto p) return;
        if (!int.TryParse(TxtQtdMov.Text, out int qtd) || qtd <= 0)
        {
            MessageBox.Show("Informe uma quantidade válida maior que zero.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var u = SessaoAtual.UsuarioLogado;
            DatabaseService.RegistrarMovimentacaoEstoque(p.Id, _tipoMovAtual, qtd, TxtMotivoMov.Text.Trim(), u?.Id, u?.Nome ?? "Sistema");
            PanelMovimentacao.Visibility = Visibility.Collapsed;
            CarregarProdutos();
            LimparFormulario();
            MessageBox.Show("Movimentação registrada com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Erro na Movimentação", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
