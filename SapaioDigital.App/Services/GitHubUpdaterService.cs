using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using SapaioDigital.App.Data;

namespace SapaioDigital.App.Services;

public record AtualizacaoInfo(
    bool Disponivel,
    string VersaoAtual,
    string NovaVersao,
    string Notas,
    string DownloadUrl,
    bool EhInstalador);

/// <summary>
/// Integração com GitHub Releases para atualização do aplicativo sem sobrescrever o banco SQLite.
/// Prioriza o instalador Inno Setup (.exe) em modo silencioso e mantém fallback para pacote portátil (.zip).
/// </summary>
public static class GitHubUpdaterService
{
    public const string RepoOwner = "digao13";
    public const string RepoName = "sapaio-digital-pdv";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    static GitHubUpdaterService()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("SapaioDigital-Updater/1.0");
        Http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static string ObterVersaoAtual()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver == null ? "1.0.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    public static async Task<AtualizacaoInfo> VerificarAtualizacaoAsync()
    {
        string versaoAtual = ObterVersaoAtual();
        string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

        using var resp = await Http.GetAsync(url);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new AtualizacaoInfo(false, versaoAtual, versaoAtual, "Nenhuma release publicada ainda no GitHub.", "", false);
        }
        resp.EnsureSuccessStatusCode();

        string json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? versaoAtual;
        string notas = root.TryGetProperty("body", out var bodyEl) ? (bodyEl.GetString() ?? "") : "";
        string installerUrl = "";
        string zipUrl = "";

        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                string dl = asset.GetProperty("browser_download_url").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    installerUrl = dl;
                else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    zipUrl = dl;
            }
        }

        bool ehInstalador = !string.IsNullOrWhiteSpace(installerUrl);
        string chosenUrl = ehInstalador ? installerUrl : zipUrl;

        bool disponivel = Version.TryParse(tag, out var vNova)
                       && Version.TryParse(versaoAtual, out var vAtual)
                       && vNova > vAtual
                       && !string.IsNullOrWhiteSpace(chosenUrl);

        return new AtualizacaoInfo(disponivel, versaoAtual, tag, notas, chosenUrl, ehInstalador);
    }

    public static async Task BaixarEAplicarAtualizacaoAsync(string downloadUrl)
    {
        // 1. Backup preventivo do banco SQLite antes de qualquer atualização
        DatabaseService.CriarBackupSeguro("pre_update_github");

        string tempRoot = Path.Combine(Path.GetTempPath(), "SapaioDigitalUpdate_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempRoot);

        bool ehInstalador = downloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

        if (ehInstalador)
        {
            string setupPath = Path.Combine(tempRoot, "SapaioDigital-Setup.exe");
            using (var stream = await Http.GetStreamAsync(downloadUrl))
            using (var fs = File.Create(setupPath))
            {
                await stream.CopyToAsync(fs);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });
            return;
        }

        // Fallback portátil (.zip)
        string zipPath = Path.Combine(tempRoot, "update.zip");
        string extractDir = Path.Combine(tempRoot, "files");
        Directory.CreateDirectory(extractDir);

        using (var stream = await Http.GetStreamAsync(downloadUrl))
        using (var fs = File.Create(zipPath))
        {
            await stream.CopyToAsync(fs);
        }

        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        string appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string exeName = Path.GetFileName(Environment.ProcessPath ?? "SapaioDigital.App.exe");
        int pid = Environment.ProcessId;

        string batPath = Path.Combine(tempRoot, "apply_update.bat");
        string batContent = $@"@echo off
timeout /t 2 /nobreak > nul
:waitloop
tasklist /FI ""PID eq {pid}"" 2>NUL | find /I /N ""{pid}"" >NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 1 /nobreak > nul
    goto waitloop
)
xcopy ""{extractDir}\*"" ""{appDir}\"" /E /Y /I /Q
start """" ""{Path.Combine(appDir, exeName)}""
del ""%~f0""
";
        await File.WriteAllTextAsync(batPath, batContent);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }
}
