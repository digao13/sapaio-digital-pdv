using System;
using System.Diagnostics;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Services;

public static class PdfVendaService
{
    static PdfVendaService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string GerarComprovanteA4Pdf(Venda venda, LojaConfig loja, bool abrirAposGerar = true)
    {
        AppPaths.EnsureDirectories();
        string vendasDir = Path.Combine(AppPaths.DataRoot, "VendasPDF");
        Directory.CreateDirectory(vendasDir);
        string filePath = Path.Combine(vendasDir, $"Venda_{venda.Id:D6}_{venda.DataHora:yyyyMMdd_HHmm}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        if (!string.IsNullOrWhiteSpace(loja.LogoPath) && File.Exists(loja.LogoPath))
                        {
                            row.ConstantItem(80).Height(65).Image(loja.LogoPath).FitArea();
                        }

                        row.RelativeItem().PaddingLeft(12).Column(info =>
                        {
                            info.Item().Text(loja.RazaoSocial).FontSize(18).Bold().FontColor("#1D1D1F");
                            if (!string.IsNullOrWhiteSpace(loja.Cnpj))
                                info.Item().Text($"CNPJ: {loja.Cnpj}").FontSize(9.5f).FontColor("#6E6E73");
                            info.Item().Text($"Telefone / WhatsApp: {loja.Telefone}").FontSize(10).FontColor("#6E6E73");
                            info.Item().Text("Comprovante de Venda (Sem Valor Fiscal)").FontSize(9).FontColor("#6E6E73");
                        });

                        row.ConstantItem(175).Border(1).BorderColor("#E0E0E0").Background("#F5F5F7").Padding(8).Column(box =>
                        {
                            box.Item().Text($"VENDA Nº {venda.Id:D6}").Bold().FontSize(11).FontColor("#1D1D1F");
                            box.Item().Text($"Data: {venda.DataHora:dd/MM/yyyy}").FontSize(9);
                            box.Item().Text($"Horário: {venda.DataHora:HH:mm:ss}").FontSize(9);
                            box.Item().Text($"Pagto: {venda.FormaPagamento}").Bold().FontSize(9).FontColor("#0066CC");
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor("#1D1D1F");
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Border(1).BorderColor("#E0E0E0").Padding(10).Row(r =>
                    {
                        r.RelativeItem().Text($"Cliente: {venda.ClienteNome}").Bold();
                        r.RelativeItem().AlignRight().Text($"Operador: {venda.UsuarioNome}");
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(36);
                            columns.RelativeColumn();
                            columns.ConstantColumn(55);
                            columns.ConstantColumn(85);
                            columns.ConstantColumn(90);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background("#1D1D1F").Padding(6).Text("#").FontColor(Colors.White).Bold();
                            header.Cell().Background("#1D1D1F").Padding(6).Text("Produto").FontColor(Colors.White).Bold();
                            header.Cell().Background("#1D1D1F").Padding(6).AlignRight().Text("Qtd").FontColor(Colors.White).Bold();
                            header.Cell().Background("#1D1D1F").Padding(6).AlignRight().Text("Vl. Unit.").FontColor(Colors.White).Bold();
                            header.Cell().Background("#1D1D1F").Padding(6).AlignRight().Text("Subtotal").FontColor(Colors.White).Bold();
                        });

                        int idx = 1;
                        foreach (var item in venda.Itens)
                        {
                            string bg = idx % 2 == 0 ? "#FAFAFC" : "#FFFFFF";
                            table.Cell().Background(bg).BorderBottom(1).BorderColor("#F0F0F0").Padding(6).Text(idx.ToString("D2"));
                            table.Cell().Background(bg).BorderBottom(1).BorderColor("#F0F0F0").Padding(6).Text(item.ProdutoNome);
                            table.Cell().Background(bg).BorderBottom(1).BorderColor("#F0F0F0").Padding(6).AlignRight().Text(item.Quantidade.ToString());
                            table.Cell().Background(bg).BorderBottom(1).BorderColor("#F0F0F0").Padding(6).AlignRight().Text($"R$ {item.PrecoUnitario:N2}");
                            table.Cell().Background(bg).BorderBottom(1).BorderColor("#F0F0F0").Padding(6).AlignRight().Text($"R$ {item.Subtotal:N2}");
                            idx++;
                        }
                    });

                    col.Item().AlignRight().Width(240).Border(1).BorderColor("#E0E0E0").Background("#F5F5F7").Padding(10).Column(tot =>
                    {
                        tot.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal:");
                            r.AutoItem().Text($"R$ {venda.Subtotal:N2}");
                        });
                        if (venda.Desconto > 0)
                        {
                            tot.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Desconto:");
                                r.AutoItem().Text($"- R$ {venda.Desconto:N2}");
                            });
                        }
                        tot.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL PAGO:").Bold().FontSize(12);
                            r.AutoItem().Text($"R$ {venda.Total:N2}").Bold().FontSize(12).FontColor("#0066CC");
                        });
                        if (venda.ValorRecebido > 0)
                        {
                            tot.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Valor Recebido:").FontSize(9);
                                r.AutoItem().Text($"R$ {venda.ValorRecebido:N2}").FontSize(9);
                            });
                            tot.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Troco:").FontSize(9);
                                r.AutoItem().Text($"R$ {venda.Troco:N2}").FontSize(9);
                            });
                        }
                    });
                });

                page.Footer().AlignCenter().Text($"{loja.RazaoSocial} • Tel: {loja.Telefone} • Obrigado pela preferência!").FontSize(8).FontColor("#6E6E73");
            });
        }).GeneratePdf(filePath);

        if (abrirAposGerar)
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        return filePath;
    }
}
