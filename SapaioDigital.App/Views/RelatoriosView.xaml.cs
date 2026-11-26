using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;
using SapaioDigital.App.Services;

namespace SapaioDigital.App.Views;

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

        TxtResumoVendas.Text = $"R$ {vendas.Sum(v => v.Total):N2}";
        int totalBaixas = movs.Where(m => m.Tipo is "SAIDA" or "VENDA").Sum(m => m.Quantidade);
        TxtResumoBaixas.Text = $"{totalBaixas} unidades";
        TxtResumoOs.Text = $"{oss.Count} OS";
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
