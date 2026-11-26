using System;
using System.Collections.Generic;

namespace SapaioDigital.App.Models;

public class LojaConfig
{
    public int Id { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public bool Configurada { get; set; }
}

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Perfil { get; set; } = "Operador";
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? CodigoBarras { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal PrecoCusto { get; set; }
    public string Unidade { get; set; } = "UN";
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}

public class MovimentacaoEstoque
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public string Tipo { get; set; } = "ENTRADA"; // ENTRADA, SAIDA, VENDA
    public int Quantidade { get; set; }
    public int SaldoAnterior { get; set; }
    public int SaldoNovo { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public DateTime DataHora { get; set; } = DateTime.Now;
}

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string TelefoneWhatsApp { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}

public class Orcamento
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteTelefone { get; set; } = string.Empty;
    public string ClienteEndereco { get; set; } = string.Empty;
    public string Equipamento { get; set; } = string.Empty;
    public string ProblemaRelatado { get; set; } = string.Empty;
    public string ServicosObservacoes { get; set; } = string.Empty;
    public decimal ValorServico { get; set; }
    public decimal ValorEstimado { get; set; }
    public List<OrcamentoItem> Itens { get; set; } = new();
    public DateTime DataHora { get; set; } = DateTime.Now;
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public string Status { get; set; } = "Aberto";
}

public class Venda
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string ClienteNome { get; set; } = "Consumidor Final";
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public DateTime DataHora { get; set; } = DateTime.Now;
    public string FormaPagamento { get; set; } = "Dinheiro";
    public decimal Subtotal { get; set; }
    public decimal Desconto { get; set; }
    public decimal Total { get; set; }
    public decimal ValorRecebido { get; set; }
    public decimal Troco { get; set; }
    public List<VendaItem> Itens { get; set; } = new();
}

public class VendaItem
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public string? CodigoBarras { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Subtotal => Quantidade * PrecoUnitario;
}
public class OrcamentoItem
{
    public int Id { get; set; }
    public int OrcamentoId { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public bool Baixado { get; set; }
    public decimal Subtotal => Quantidade * PrecoUnitario;
}
