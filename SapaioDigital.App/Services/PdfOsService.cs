using System;
using System.Diagnostics;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SapaioDigital.App.Helpers;
using SapaioDigital.App.Models;

namespace SapaioDigital.App.Services;

public static class PdfOsService
{
    static PdfOsService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static string GerarOrdemServicoPdf(Orcamento os, LojaConfig loja, bool abrirAposGerar = true)
    {
        AppPaths.EnsureDirectories();
        string filePath = Path.Combine(AppPaths.OsPdfDir, $"OS_{os.Id:D5}_{os.DataHora:yyyyMMdd_HHmm}.pdf");

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
                            info.Item().Text(loja.RazaoSocial).FontSize(18).Bold().FontColor("#1E293B");
                            info.Item().Text($"Telefone / WhatsApp: {loja.Telefone}").FontSize(10).FontColor("#475569");
                            info.Item().Text("AssistÃªncia TÃ©cnica e InformÃ¡tica").FontSize(9).FontColor("#64748B");
                        });

                        row.ConstantItem(165).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(8).Column(box =>
                        {
                            box.Item().Text($"ORDEM DE SERVIÃ‡O NÂº {os.Id:D5}").Bold().FontSize(11).FontColor("#0F172A");
                            box.Item().Text($"Data: {os.DataHora:dd/MM/yyyy}").FontSize(9);
                            box.Item().Text($"HorÃ¡rio: {os.DataHora:HH:mm:ss}").FontSize(9);
                            box.Item().Text($"Status: {os.Status}").Bold().FontSize(9).FontColor("#2563EB");
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor("#1E293B");
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(14);

                    // Dados do Cliente
                    col.Item().Border(1).BorderColor("#E2E8F0").Padding(10).Column(c =>
                    {
                        c.Item().Text("DADOS DO CLIENTE").Bold().FontSize(10).FontColor("#1E293B");
                        c.Item().PaddingTop(4).Text($"Nome: {os.ClienteNome}");
                        c.Item().Text($"Telefone / WhatsApp: {os.ClienteTelefone}");
                        c.Item().Text($"EndereÃ§o: {os.ClienteEndereco}");
                    });

                    // Equipamento & ResponsÃ¡vel
                    col.Item().Border(1).BorderColor("#E2E8F0").Padding(10).Column(c =>
                    {
                        c.Item().Text("INFORMAÃ‡Ã•ES DO ATENDIMENTO").Bold().FontSize(10).FontColor("#1E293B");
                        c.Item().PaddingTop(4).Text($"Equipamento / Dispositivo: {(string.IsNullOrWhiteSpace(os.Equipamento) ? "NÃ£o informado" : os.Equipamento)}");
                        c.Item().Text($"OrÃ§amento / OS feito por: {os.UsuarioNome}").Bold();
                        c.Item().Text($"Data e Hora do Registro: {os.DataHora:dd/MM/yyyy 'Ã s' HH:mm:ss}");
                    });

                    // Problema Relatado
                    col.Item().Border(1).BorderColor("#E2E8F0").Padding(10).Column(c =>
                    {
                        c.Item().Text("PROBLEMA RELATADO / DEFEITO").Bold().FontSize(10).FontColor("#1E293B");
                        c.Item().PaddingTop(4).Text(os.ProblemaRelatado);
                    });

                    // ServiÃ§os / ObservaÃ§Ãµes TÃ©cnicas
                    col.Item().Border(1).BorderColor("#E2E8F0").Padding(10).Column(c =>
                    {
                        c.Item().Text("SERVIÃ‡OS PREVISTOS / OBSERVAÃ‡Ã•ES TÃ‰CNICAS").Bold().FontSize(10).FontColor("#1E293B");
                        c.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(os.ServicosObservacoes) ? "Sem observaÃ§Ãµes adicionais." : os.ServicosObservacoes);
                    });

                    // Valor Estimado
                    col.Item().AlignRight().Background("#EFF6FF").Border(1).BorderColor("#BFDBFE").Padding(10).Row(r =>
                    {
                        r.AutoItem().Text("VALOR ESTIMADO / TOTAL:  ").Bold().FontSize(12).FontColor("#1E3A8A");
                        r.AutoItem().Text($"R$ {os.ValorEstimado:N2}").Bold().FontSize(13).FontColor("#1D4ED8");
                    });

                    // Assinaturas
                    col.Item().PaddingTop(36).Row(r =>
                    {
                        r.RelativeItem().PaddingRight(20).Column(s =>
                        {
                            s.Item().LineHorizontal(1).LineColor("#475569");
                            s.Item().PaddingTop(4).AlignCenter().Text(os.ClienteNome).FontSize(9);
                            s.Item().AlignCenter().Text("Assinatura do Cliente").FontSize(8).FontColor("#64748B");
                        });

                        r.RelativeItem().PaddingLeft(20).Column(s =>
                        {
                            s.Item().LineHorizontal(1).LineColor("#475569");
                            s.Item().PaddingTop(4).AlignCenter().Text($"{loja.RazaoSocial} ({os.UsuarioNome})").FontSize(9);
                            s.Item().AlignCenter().Text("ResponsÃ¡vel TÃ©cnico / Loja").FontSize(8).FontColor("#64748B");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span($"{loja.RazaoSocial} â€¢ Tel: {loja.Telefone} â€¢ Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#64748B");
                });
            });
        }).GeneratePdf(filePath);

        if (abrirAposGerar)
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        return filePath;
    }
}

