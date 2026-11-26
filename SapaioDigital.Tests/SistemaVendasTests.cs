using System;
using System.IO;
using System.Linq;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;
using SapaioDigital.App.Services;
using Xunit;

namespace SapaioDigital.Tests;

public class SistemaVendasTests
{
    [Fact]
    public void FluxoCompleto_BancoEstoquePdvOsPdf_DeveFuncionarSemErros()
    {
        AppPaths.OverrideDataRootForTests(Path.Combine(Path.GetTempPath(), "SapaioTests_" + Guid.NewGuid().ToString("N")[..6]));
        DatabaseService.Initialize();

        var loja = new LojaConfig
        {
            RazaoSocial = "Sampaio Digital Teste",
            Telefone = "(11) 99999-9999",
            LogoPath = "",
            Configurada = true
        };
        DatabaseService.SalvarLojaConfig(loja);
        var lojaSalva = DatabaseService.ObterLojaConfig();
        Assert.NotNull(lojaSalva);
        Assert.True(lojaSalva!.Configurada);

        string sufixo = Guid.NewGuid().ToString("N")[..6];
        var prod = new Produto
        {
            Nome = $"SSD NVMe 500GB {sufixo}",
            CodigoBarras = $"789{sufixo}",
            Quantidade = 10,
            PrecoVenda = 250.00m,
            PrecoCusto = 160.00m,
            Unidade = "UN"
        };
        DatabaseService.SalvarProduto(prod, null, "Tester");
        Assert.True(prod.Id > 0);

        DatabaseService.RegistrarMovimentacaoEstoque(prod.Id, "ENTRADA", 5, "Compra fornecedor", null, "Tester");
        DatabaseService.RegistrarMovimentacaoEstoque(prod.Id, "SAIDA", 2, "Uso interno", null, "Tester");

        var prodAtualizado = DatabaseService.ObterProdutoPorCodigoBarras(prod.CodigoBarras!);
        Assert.Equal(13, prodAtualizado!.Quantidade);

        var venda = new Venda
        {
            ClienteNome = "Consumidor Teste",
            UsuarioNome = "Tester",
            DataHora = DateTime.Now,
            FormaPagamento = "PIX",
            Subtotal = 500.00m,
            Desconto = 0m,
            Total = 500.00m,
            Itens =
            {
                new VendaItem
                {
                    ProdutoId = prod.Id,
                    ProdutoNome = prod.Nome,
                    CodigoBarras = prod.CodigoBarras,
                    Quantidade = 2,
                    PrecoUnitario = 250.00m
                }
            }
        };
        int vendaId = DatabaseService.FinalizarVenda(venda);
        Assert.True(vendaId > 0);

        string cupom = CupomNaoFiscalService.GerarTextoCupom(venda, lojaSalva);
        Assert.Contains("CUPOM NAO FISCAL", cupom);

        string pdfVendaA4 = PdfVendaService.GerarComprovanteA4Pdf(venda, lojaSalva, abrirAposGerar: false);
        Assert.True(File.Exists(pdfVendaA4));
        Assert.True(new FileInfo(pdfVendaA4).Length > 1000);

        var cliente = new Cliente
        {
            Nome = $"Cliente OS {sufixo}",
            TelefoneWhatsApp = "(11) 98888-7777",
            Endereco = "Rua das Flores, 123"
        };
        DatabaseService.SalvarCliente(cliente);

        var os = new Orcamento
        {
            ClienteId = cliente.Id,
            ClienteNome = cliente.Nome,
            ClienteTelefone = cliente.TelefoneWhatsApp,
            ClienteEndereco = cliente.Endereco,
            Equipamento = "Notebook Dell Inspiron",
            ProblemaRelatado = "Não liga, fonte em curto",
            ServicosObservacoes = "Troca do jack DC",
            ValorServico = 180.00m,
            ValorEstimado = 430.00m,
            Itens = { new OrcamentoItem { ProdutoId = prod.Id, ProdutoNome = prod.Nome, Quantidade = 1, PrecoUnitario = 250.00m } },
            DataHora = DateTime.Now,
            UsuarioNome = "Tester",
            Status = "Aberto"
        };
        DatabaseService.SalvarOrcamento(os);

        var prodAposOs = DatabaseService.ObterProdutoPorCodigoBarras(prod.CodigoBarras!);
        Assert.Equal(10, prodAposOs!.Quantidade);

        string pdfOs = PdfOsService.GerarOrdemServicoPdf(os, lojaSalva, abrirAposGerar: false);
        Assert.True(File.Exists(pdfOs));

        string backupPath = BackupService.ExecutarBackup("teste_unitario");
        Assert.True(File.Exists(backupPath));
        Assert.True(new FileInfo(backupPath).Length > 0);

        // Validações
        Assert.True(InputValidator.TelefoneValido("(21) 96616-0221"));
        Assert.False(InputValidator.TelefoneValido("123"));
        Assert.True(InputValidator.TryParseDecimalPtBr("150,50", out decimal dec) && dec == 150.50m);
    }
}
