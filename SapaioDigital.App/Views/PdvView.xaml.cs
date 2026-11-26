using System;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;
using SapaioDigital.App.Services;

namespace SapaioDigital.App.Views;

public partial class PdvView : UserControl
{
    private readonly ObservableCollection<VendaItem> _carrinho = new();

    public PdvView()
    {
        InitializeComponent();
        GridCarrinho.ItemsSource = _carrinho;
        InputValidator.AplicarSomenteInteiro(TxtQtdAdd);
        InputValidator.AplicarSomenteDecimal(TxtDesconto);
        InputValidator.AplicarSomenteDecimal(TxtRecebido);

        CarregarLoja();
        CarregarClientes();
        CarregarImpressoras();
        AtualizarSugestoes();
        AtualizarTotais();
        Loaded += (_, _) => TxtBuscaProduto.Focus();
    }

    private void CmbPadraoImpressao_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelImpressoraTermica == null) return;
        PanelImpressoraTermica.Visibility = CmbPadraoImpressao.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
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
        var clientes = DatabaseService.ListarClientes();
        clientes.Insert(0, new Cliente { Id = 0, Nome = "Consumidor Final" });
        CmbCliente.ItemsSource = clientes;
        CmbCliente.SelectedIndex = 0;
    }

    private void CarregarImpressoras()
    {
        foreach (string printer in PrinterSettings.InstalledPrinters)
            CmbImpressora.Items.Add(printer);

        var padrao = new PrinterSettings().PrinterName;
        if (!string.IsNullOrWhiteSpace(padrao) && CmbImpressora.Items.Contains(padrao))
            CmbImpressora.SelectedItem = padrao;
        else if (CmbImpressora.Items.Count > 0)
            CmbImpressora.SelectedIndex = 0;
    }

    private void AtualizarSugestoes() => GridSugestoes.ItemsSource = DatabaseService.ListarProdutos(TxtBuscaProduto.Text);

    private void TxtBuscaProduto_TextChanged(object sender, TextChangedEventArgs e) => AtualizarSugestoes();

    private void TxtBuscaProduto_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AdicionarPorBusca();
    }

    private void BtnAdicionar_Click(object sender, RoutedEventArgs e) => AdicionarPorBusca();

    private void GridSugestoes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GridSugestoes.SelectedItem is Produto p)
            AdicionarProdutoAoCarrinho(p);
    }

    private void AdicionarPorBusca()
    {
        string termo = TxtBuscaProduto.Text.Trim();
        if (string.IsNullOrWhiteSpace(termo)) return;

        var prod = DatabaseService.ObterProdutoPorCodigoBarras(termo)
                ?? DatabaseService.ListarProdutos(termo).FirstOrDefault();

        if (prod == null)
        {
            MessageBox.Show("Produto não encontrado.", "PDV", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AdicionarProdutoAoCarrinho(prod);
        TxtBuscaProduto.Clear();
        TxtBuscaProduto.Focus();
    }

    private void AdicionarProdutoAoCarrinho(Produto prod)
    {
        if (!int.TryParse(TxtQtdAdd.Text, out int qtd) || qtd <= 0) qtd = 1;

        var existente = _carrinho.FirstOrDefault(x => x.ProdutoId == prod.Id);
        int novaQtd = (existente?.Quantidade ?? 0) + qtd;

        if (novaQtd > prod.Quantidade)
        {
            MessageBox.Show($"Estoque insuficiente para '{prod.Nome}'. Disponível: {prod.Quantidade}.", "Estoque", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (existente != null)
        {
            existente.Quantidade = novaQtd;
            GridCarrinho.Items.Refresh();
        }
        else
        {
            _carrinho.Add(new VendaItem
            {
                ProdutoId = prod.Id,
                ProdutoNome = prod.Nome,
                CodigoBarras = prod.CodigoBarras,
                Quantidade = qtd,
                PrecoUnitario = prod.PrecoVenda
            });
        }

        TxtQtdAdd.Text = "1";
        AtualizarTotais();
    }

    private void BtnRemoverItem_Click(object sender, RoutedEventArgs e)
    {
        if (GridCarrinho.SelectedItem is VendaItem item)
        {
            _carrinho.Remove(item);
            AtualizarTotais();
        }
    }

    private void TxtValores_TextChanged(object sender, TextChangedEventArgs e) => AtualizarTotais();

    private void AtualizarTotais()
    {
        if (TxtSubtotal == null) return;
        decimal subtotal = _carrinho.Sum(x => x.Subtotal);
        InputValidator.TryParseDecimalPtBr(TxtDesconto.Text, out decimal desconto);
        InputValidator.TryParseDecimalPtBr(TxtRecebido.Text, out decimal recebido);

        decimal total = Math.Max(0, subtotal - desconto);
        decimal troco = recebido > total ? recebido - total : 0;

        TxtSubtotal.Text = $"R$ {subtotal:N2}";
        TxtTotal.Text = $"R$ {total:N2}";
        TxtTroco.Text = $"R$ {troco:N2}";
    }

    private void BtnLimparCarrinho_Click(object sender, RoutedEventArgs e)
    {
        _carrinho.Clear();
        TxtDesconto.Text = "0,00";
        TxtRecebido.Text = "0,00";
        AtualizarTotais();
    }

    private void BtnFinalizar_Click(object sender, RoutedEventArgs e)
    {
        if (_carrinho.Count == 0)
        {
            MessageBox.Show("Adicione produtos ao carrinho antes de finalizar.", "PDV", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal subtotal = _carrinho.Sum(x => x.Subtotal);
        if (!InputValidator.TryParseDecimalPtBr(TxtDesconto.Text, out decimal desconto) || desconto < 0)
        {
            MessageBox.Show("Valor de desconto inválido.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!InputValidator.TryParseDecimalPtBr(TxtRecebido.Text, out decimal recebido) || recebido < 0)
        {
            MessageBox.Show("Valor recebido inválido.", "Validação", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal total = Math.Max(0, subtotal - desconto);
        decimal troco = recebido > total ? recebido - total : 0;

        var clienteSel = CmbCliente.SelectedItem as Cliente;
        var usuario = SessaoAtual.UsuarioLogado;

        var venda = new Venda
        {
            ClienteId = clienteSel != null && clienteSel.Id > 0 ? clienteSel.Id : null,
            ClienteNome = clienteSel != null && clienteSel.Id > 0 ? clienteSel.Nome : "Consumidor Final",
            UsuarioId = usuario?.Id,
            UsuarioNome = usuario?.Nome ?? "Operador",
            DataHora = DateTime.Now,
            FormaPagamento = (CmbPagamento.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Dinheiro",
            Subtotal = subtotal,
            Desconto = desconto,
            Total = total,
            ValorRecebido = recebido,
            Troco = troco,
            Itens = _carrinho.ToList()
        };

        try
        {
            DatabaseService.FinalizarVenda(venda);
            var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sampaio Digital" };

            if (ChkImprimirCupom.IsChecked == true)
            {
                try
                {
                    if (CmbPadraoImpressao.SelectedIndex == 1)
                        PdfVendaService.GerarComprovanteA4Pdf(venda, loja, abrirAposGerar: true);
                    else
                        CupomNaoFiscalService.ImprimirCupom(venda, loja, CmbImpressora.SelectedItem?.ToString());
                }
                catch (Exception impEx)
                {
                    MessageBox.Show($"Venda #{venda.Id} concluída e estoque baixado, mas houve erro ao imprimir:\n{impEx.Message}", "Aviso de Impressão", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            _carrinho.Clear();
            TxtDesconto.Text = "0,00";
            TxtRecebido.Text = "0,00";
            AtualizarTotais();
            AtualizarSugestoes();
            MessageBox.Show($"Venda #{venda.Id:D6} finalizada com sucesso!", "PDV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível finalizar a venda:\n{ex.Message}", "Erro no PDV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
