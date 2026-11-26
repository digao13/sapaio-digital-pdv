using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;
using SapaioDigital.App.Services;

namespace SapaioDigital.App.Views;

public record BarraDashboardItem(string Rotulo, string ValorTexto, double Percentual);

public partial class RelatoriosView : UserControl
{
    public RelatoriosView()
    {
        InitializeComponent();
        DpInicio.SelectedDate = DateTime.Today.AddDays(-30);
        DpFim.SelectedDate = DateTime.Today;
        CarregarDados();
    }

    private void BtnFiltrar_Click(object sender, RoutedEventArgs e) => CarregarDados();

    private void CarregarDados()
    {
        DateTime? ini = DpInicio.SelectedDate;
        DateTime? fim = DpFim.SelectedDate;

        var movs = DatabaseService.ListarMovimentacoes(ini, fim);
        var vendas = DatabaseService.ListarVendas(ini, fim);
        var oss = DatabaseService.ListarOrcamentos(ini, fim);

        GridMovimentacoes.ItemsSource = movs;
        GridVendas.ItemsSource = vendas;
        GridOrcamentos.ItemsSource = oss;

        decimal totalVendas = vendas.Sum(v => v.Total);
        decimal ticketMedio = vendas.Count > 0 ? totalVendas / vendas.Count : 0;
        TxtResumoVendas.Text = $"R$ {totalVendas:N2}";
        TxtResumoTicket.Text = $"R$ {ticketMedio:N2}";

        int totalBaixas = movs.Where(m => m.Tipo is "SAIDA" or "VENDA").Sum(m => m.Quantidade);
        TxtResumoBaixas.Text = $"{totalBaixas} unidades";
        TxtResumoOs.Text = $"{oss.Count} OS";

        // Dashboard: Formas de Pagamento
        if (vendas.Count == 0 || totalVendas <= 0)
        {
            ListPagamentosDash.ItemsSource = null;
            TxtSemDadosPagto.Visibility = Visibility.Visible;
        }
        else
        {
            TxtSemDadosPagto.Visibility = Visibility.Collapsed;
            var pagtos = vendas
                .GroupBy(v => v.FormaPagamento)
                .Select(g => new
                {
                    Forma = g.Key,
                    Total = g.Sum(x => x.Total),
                    Qtd = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .Select(x => new BarraDashboardItem(
                    $"{x.Forma} ({x.Qtd}x)",
                    $"R$ {x.Total:N2}",
                    Math.Round((double)(x.Total / totalVendas * 100m), 1)))
                .ToList();
            ListPagamentosDash.ItemsSource = pagtos;
        }

        // Dashboard: Top 5 Produtos Vendidos
        var vendasMovs = movs.Where(m => m.Tipo == "VENDA").ToList();
        if (vendasMovs.Count == 0)
        {
            ListTopProdutosDash.ItemsSource = null;
            TxtSemDadosProd.Visibility = Visibility.Visible;
        }
        else
        {
            TxtSemDadosProd.Visibility = Visibility.Collapsed;
            var top = vendasMovs
                .GroupBy(m => m.ProdutoNome)
                .Select(g => new { Nome = g.Key, Qtd = g.Sum(x => x.Quantidade) })
                .OrderByDescending(x => x.Qtd)
                .Take(5)
                .ToList();
            int maxQtd = Math.Max(1, top.Max(x => x.Qtd));
            ListTopProdutosDash.ItemsSource = top
                .Select(x => new BarraDashboardItem(x.Nome, $"{x.Qtd} un.", Math.Round(x.Qtd * 100.0 / maxQtd, 1)))
                .ToList();
        }
    }

    private void BtnReimprimirCupom_Click(object sender, RoutedEventArgs e)
    {
        if (GridVendas.SelectedItem is not Venda v)
        {
            MessageBox.Show("Selecione uma venda na tabela.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vendaCompleta = DatabaseService.ObterVendaCompleta(v.Id);
        if (vendaCompleta == null) return;

        var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sampaio Digital" };
        try
        {
            CupomNaoFiscalService.ImprimirCupom(vendaCompleta, loja);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao imprimir cupom: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnReimprimirA4_Click(object sender, RoutedEventArgs e)
    {
        if (GridVendas.SelectedItem is not Venda v)
        {
            MessageBox.Show("Selecione uma venda na tabela.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vendaCompleta = DatabaseService.ObterVendaCompleta(v.Id);
        if (vendaCompleta == null) return;

        var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sampaio Digital" };
        try
        {
            PdfVendaService.GerarComprovanteA4Pdf(vendaCompleta, loja, abrirAposGerar: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar PDF A4: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnReimprimirOs_Click(object sender, RoutedEventArgs e)
    {
        if (GridOrcamentos.SelectedItem is not Orcamento os)
        {
            MessageBox.Show("Selecione uma OS na tabela.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var loja = SessaoAtual.Loja ?? DatabaseService.ObterLojaConfig() ?? new LojaConfig { RazaoSocial = "Sampaio Digital" };
        try
        {
            PdfOsService.GerarOrdemServicoPdf(os, loja, abrirAposGerar: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar PDF: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
