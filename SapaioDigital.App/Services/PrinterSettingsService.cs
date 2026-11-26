using System;
using System.IO;
using System.Text.Json;

namespace SapaioDigital.App.Services;

public class PrinterConfig
{
    public string ImpressoraPadrao { get; set; } = "";
    public string FormatoCupom { get; set; } = "80mm";
    public int CopiasPadrao { get; set; } = 1;
    public bool ImprimirAutomaticamente { get; set; } = false;
}

public static class PrinterSettingsService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SapaioDigital",
        "impressora.json");

    public static PrinterConfig CarregarConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<PrinterConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
        }
        return new PrinterConfig();
    }

    public static void SalvarConfig(PrinterConfig cfg)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
        }
    }
}