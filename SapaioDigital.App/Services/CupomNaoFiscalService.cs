using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Services;

/// <summary>
/// Impressão de cupom não fiscal em layout de mercado (bobina térmica 80mm / 48 colunas).
/// </summary>
public static class CupomNaoFiscalService
{
    public static string GerarTextoCupom(Venda venda, LojaConfig loja)
    {
        const int largura = 44;
        var sb = new StringBuilder();
        string linha = new string('-', largura);

        sb.AppendLine(Centralizar(loja.RazaoSocial.ToUpperInvariant(), largura));
        if (!string.IsNullOrWhiteSpace(loja.Cnpj))
            sb.AppendLine(Centralizar($"CNPJ: {loja.Cnpj}", largura));
        sb.AppendLine(Centralizar($"Tel: {loja.Telefone}", largura));
        sb.AppendLine(linha);
        sb.AppendLine(Centralizar("CUPOM NAO FISCAL - COMPROVANTE", largura));
        sb.AppendLine(linha);
        sb.AppendLine($"Venda Nº: {venda.Id:D6}   Data: {venda.DataHora:dd/MM/yyyy HH:mm}");
        sb.AppendLine($"Operador: {Truncar(venda.UsuarioNome, 34)}");
        sb.AppendLine($"Cliente : {Truncar(venda.ClienteNome, 34)}");
        sb.AppendLine(linha);
        sb.AppendLine("ITEM  DESCRICAO");
        sb.AppendLine("      QTD x VL.UNIT              SUBTOTAL");
        sb.AppendLine(linha);

        int idx = 1;
        foreach (var item in venda.Itens)
        {
            string desc = Truncar(item.ProdutoNome, largura - 6);
            sb.AppendLine($"{idx:D3}   {desc}");
            string qtdUnit = $"      {item.Quantidade} x {item.PrecoUnitario:N2}";
            string sub = $"R$ {item.Subtotal:N2}";
            int espacos = Math.Max(1, largura - qtdUnit.Length - sub.Length);
            sb.AppendLine(qtdUnit + new string(' ', espacos) + sub);
            idx++;
        }

        sb.AppendLine(linha);
        sb.AppendLine(FormatarLinhaValor("SUBTOTAL:", $"R$ {venda.Subtotal:N2}", largura));
        if (venda.Desconto > 0)
            sb.AppendLine(FormatarLinhaValor("DESCONTO:", $"- R$ {venda.Desconto:N2}", largura));
        sb.AppendLine(FormatarLinhaValor("TOTAL A PAGAR:", $"R$ {venda.Total:N2}", largura));
        sb.AppendLine(linha);
        sb.AppendLine($"Forma Pagto: {venda.FormaPagamento}");
        if (venda.ValorRecebido > 0)
        {
            sb.AppendLine(FormatarLinhaValor("Valor Recebido:", $"R$ {venda.ValorRecebido:N2}", largura));
            sb.AppendLine(FormatarLinhaValor("Troco:", $"R$ {venda.Troco:N2}", largura));
        }
        sb.AppendLine(linha);
        sb.AppendLine(Centralizar("*** DOCUMENTO SEM VALOR FISCAL ***", largura));
        sb.AppendLine(Centralizar("Obrigado pela preferencia!", largura));
        sb.AppendLine();
        sb.AppendLine();
        return sb.ToString();
    }

    public static void ImprimirCupom(Venda venda, LojaConfig loja, string? nomeImpressora = null)
    {
        string texto = GerarTextoCupom(venda, loja);

        using var printDoc = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(nomeImpressora))
            printDoc.PrinterSettings.PrinterName = nomeImpressora;

        // Bobina 80mm (~315 centésimos de polegada)
        printDoc.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);

        printDoc.PrintPage += (_, e) =>
        {
            if (e.Graphics == null) return;
            float y = e.MarginBounds.Top;
            float left = e.MarginBounds.Left;

            // Imprime logo pequena no topo se existir
            if (!string.IsNullOrWhiteSpace(loja.LogoPath) && File.Exists(loja.LogoPath))
            {
                try
                {
                    using var img = Image.FromFile(loja.LogoPath);
                    int logoH = 42;
                    int logoW = Math.Min(160, (int)(img.Width * (logoH / (float)img.Height)));
                    float logoX = left + Math.Max(0, (260 - logoW) / 2f);
                    e.Graphics.DrawImage(img, logoX, y, logoW, logoH);
                    y += logoH + 6;
                }
                catch
                {
                    // Segue sem logo caso o formato não seja suportado pelo GDI+
                }
            }

            using var font = new Font("Consolas", 8.5f, FontStyle.Regular);
            e.Graphics.DrawString(texto, font, Brushes.Black, left, y);
            e.HasMorePages = false;
        };

        printDoc.Print();
    }

    private static string Centralizar(string texto, int largura)
    {
        if (texto.Length >= largura) return texto[..largura];
        int pad = (largura - texto.Length) / 2;
        return new string(' ', pad) + texto;
    }

    private static string Truncar(string texto, int max)
        => texto.Length <= max ? texto : texto[..max];

    private static string FormatarLinhaValor(string label, string valor, int largura)
    {
        int espacos = Math.Max(1, largura - label.Length - valor.Length);
        return label + new string(' ', espacos) + valor;
    }
}
