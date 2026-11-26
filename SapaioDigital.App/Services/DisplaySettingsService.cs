using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace SapaioDigital.App.Services;

public class DisplayConfig
{
    public string ModoResolucao { get; set; } = "Auto";
    public double EscalaUi { get; set; } = 0;
}

public static class DisplaySettingsService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SapaioDigital",
        "display.json");

    public static event Action? ConfiguracaoAlterada;

    public static DisplayConfig CarregarConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<DisplayConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
        }
        return new DisplayConfig();
    }

    public static void SalvarConfig(DisplayConfig cfg)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
            ConfiguracaoAlterada?.Invoke();
        }
        catch
        {
        }
    }

    public static double CalcularEscalaAutomatica(double largura, double altura)
    {
        if (largura < 1200 || altura < 700) return 0.85;
        if (largura <= 1366 || altura <= 768) return 0.90;
        if (largura <= 1536 || altura <= 864) return 0.95;
        return 1.00;
    }

    public static void AplicarNaJanela(Window window, FrameworkElement rootElement, bool ajustarTamanhoJanela = true)
    {
        var workArea = SystemParameters.WorkArea;
        double workW = Math.Max(800, workArea.Width);
        double workH = Math.Max(540, workArea.Height);

        window.MinWidth = Math.Min(820, workW - 16);
        window.MinHeight = Math.Min(540, workH - 16);

        var cfg = CarregarConfig();

        if (ajustarTamanhoJanela)
        {
            switch (cfg.ModoResolucao)
            {
                case "Maximizado":
                    window.WindowState = WindowState.Maximized;
                    break;
                case "1280x720":
                    AplicarTamanhoJanela(window, 1280, 720, workArea);
                    break;
                case "1366x768":
                    AplicarTamanhoJanela(window, 1366, 768, workArea);
                    break;
                case "1600x900":
                    AplicarTamanhoJanela(window, 1600, 900, workArea);
                    break;
                case "1920x1080":
                    AplicarTamanhoJanela(window, 1920, 1080, workArea);
                    break;
                default:
                    window.WindowState = WindowState.Maximized;
                    break;
            }
        }

        double larguraEfetiva = window.WindowState == WindowState.Maximized
            ? workW
            : (window.ActualWidth > 100 ? window.ActualWidth : window.Width);
        double alturaEfetiva = window.WindowState == WindowState.Maximized
            ? workH
            : (window.ActualHeight > 100 ? window.ActualHeight : window.Height);

        double escala = cfg.EscalaUi > 0
            ? Math.Clamp(cfg.EscalaUi, 0.75, 1.30)
            : CalcularEscalaAutomatica(larguraEfetiva, alturaEfetiva);

        if (rootElement.LayoutTransform is ScaleTransform st && Math.Abs(st.ScaleX - escala) < 0.005)
            return;
        if (Math.Abs(escala - 1.0) < 0.005 && rootElement.LayoutTransform == Transform.Identity)
            return;

        rootElement.LayoutTransform = Math.Abs(escala - 1.0) < 0.005
            ? Transform.Identity
            : new ScaleTransform(escala, escala);
    }

    private static void AplicarTamanhoJanela(Window window, double targetW, double targetH, Rect workArea)
    {
        window.WindowState = WindowState.Normal;
        window.Width = Math.Min(targetW, workArea.Width);
        window.Height = Math.Min(targetH, workArea.Height);
        window.Left = workArea.Left + Math.Max(0, (workArea.Width - window.Width) / 2);
        window.Top = workArea.Top + Math.Max(0, (workArea.Height - window.Height) / 2);
    }
}