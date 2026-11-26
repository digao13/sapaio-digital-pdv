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
        // Inicializa o SQLite em %LocalAppData%\SapaioDigital
        DatabaseService.Initialize();

        // 1. Configuração da Loja
        var loja = new LojaConfig
        {
            RazaoSocial = "Sapaio Digital Teste",
            Telefone = "(11) 99999-9999",
            LogoPath = "",
            Configurada = true
        };
        DatabaseService.SalvarLojaConfig(loja);
        var lojaSalva = DatabaseService.ObterLojaConfig();
        Assert.NotNull(lojaSalva);
        Assert.True(lojaSalva!.Configurada);

        // 2. Produto + Entrada e Saída
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
        Assert.NotNull(prodAtualizado);
        Assert.Equal(13, prodAtualizado!.Quantidade);

        // 3. PDV (Venda com baixa automática + Cupom Não Fiscal)
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

        var prodAposVenda = DatabaseService.ObterProdutoPorCodigoBarras(prod.CodigoBarras!);
        Assert.Equal(11, prodAposVenda!.Quantidade);

        string cupom = CupomNaoFiscalService.GerarTextoCupom(venda, lojaSalva);
        Assert.Contains("CUPOM NAO FISCAL", cupom);
        Assert.Contains(lojaSalva.RazaoSocial.ToUpperInvariant(), cupom);

        // 4. Cliente + Ordem de Serviço + PDF
        var cliente = new Cliente
        {
            Nome = $"Cliente OS {sufixo}",
            TelefoneWhatsApp = "(11) 98888-7777",
            Endereco = "Rua das Flores, 123"
        };
        DatabaseService.SalvarCliente(cliente);
        Assert.True(cliente.Id > 0);

        var os = new Orcamento
        {
            ClienteId = cliente.Id,
            ClienteNome = cliente.Nome,
            ClienteTelefone = cliente.TelefoneWhatsApp,
            ClienteEndereco = cliente.Endereco,
            Equipamento = "Notebook Dell Inspiron",
            ProblemaRelatado = "Não liga, fonte em curto",
            ServicosObservacoes = "Troca do jack DC e limpeza interna",
            ValorEstimado = 180.00m,
            DataHora = DateTime.Now,
            UsuarioNome = "Tester",
            Status = "Aberto"
        };
        int osId = DatabaseService.SalvarOrcamento(os);
        Assert.True(osId > 0);

        string pdfPath = PdfOsService.GerarOrdemServicoPdf(os, lojaSalva, abrirAposGerar: false);
        Assert.True(File.Exists(pdfPath));
        Assert.True(new FileInfo(pdfPath).Length > 1000);

        // 5. Backup seguro via VACUUM INTO
        string backupPath = DatabaseService.CriarBackupSeguro("teste_unitario");
        Assert.True(File.Exists(backupPath));
        Assert.True(new FileInfo(backupPath).Length > 0);
    }
}
