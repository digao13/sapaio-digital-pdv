using System;
using System.IO;
using System.Text.Json;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;

namespace SapaioDigital.App.Services;

public class BackupConfig
{
    public string PastaDestino { get; set; } = AppPaths.BackupsDir;
    public string Plataforma { get; set; } = "Local";
    public bool BackupDiarioAutomatico { get; set; } = true;
    public string UltimaDataBackup { get; set; } = ""; // yyyy-MM-dd
    public string UltimoArquivoBackup { get; set; } = "";
}

/// <summary>
/// Gerencia backups locais e em nuvem via pastas sincronizadas (OneDrive, Dropbox, Google Drive)
/// sem necessidade de autenticação OAuth ou chaves de API.
/// </summary>
public static class BackupService
{
    private static string ConfigPath => Path.Combine(AppPaths.DataRoot, "backup_config.json");

    public static BackupConfig CarregarConfig()
    {
        try
        {
            AppPaths.EnsureDirectories();
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<BackupConfig>(json) ?? new BackupConfig();
            }
        }
        catch { }

        return new BackupConfig();
    }

    public static void SalvarConfig(BackupConfig config)
    {
        AppPaths.EnsureDirectories();
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static string? DetectarPastaOneDrive()
    {
        string?[] candidatos =
        {
            Environment.GetEnvironmentVariable("OneDriveConsumer"),
            Environment.GetEnvironmentVariable("OneDrive"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive")
        };

        foreach (var dir in candidatos)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                return Path.Combine(dir, "SapaioDigital_Backups");
        }
        return null;
    }

    public static string? DetectarPastaDropbox()
    {
        string userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox");
        if (Directory.Exists(userDir))
            return Path.Combine(userDir, "SapaioDigital_Backups");

        string infoJson = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dropbox", "info.json");
        if (File.Exists(infoJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(infoJson));
                if (doc.RootElement.TryGetProperty("personal", out var p) && p.TryGetProperty("path", out var pathEl))
                {
                    string? path = pathEl.GetString();
                    if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                        return Path.Combine(path, "SapaioDigital_Backups");
                }
            }
            catch { }
        }
        return null;
    }

    public static string? DetectarPastaGoogleDrive()
    {
        string[] candidatos =
        {
            @"G:\Meu Drive",
            @"G:\My Drive",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Meu Drive")
        };

        foreach (var dir in candidatos)
        {
            if (Directory.Exists(dir))
                return Path.Combine(dir, "SapaioDigital_Backups");
        }
        return null;
    }

    public static string ExecutarBackup(string motivo = "manual")
    {
        var config = CarregarConfig();
        string backupInterno = DatabaseService.CriarBackupSeguro(motivo);

        string pastaDestino = string.IsNullOrWhiteSpace(config.PastaDestino) ? AppPaths.BackupsDir : config.PastaDestino;
        Directory.CreateDirectory(pastaDestino);

        string destinoFinal = Path.Combine(pastaDestino, Path.GetFileName(backupInterno));
        if (!string.Equals(Path.GetFullPath(backupInterno), Path.GetFullPath(destinoFinal), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(backupInterno, destinoFinal, overwrite: true);
        }

        config.UltimaDataBackup = DateTime.Today.ToString("yyyy-MM-dd");
        config.UltimoArquivoBackup = destinoFinal;
        SalvarConfig(config);

        return destinoFinal;
    }

    public static void ExecutarBackupDiarioSeNecessario()
    {
        var config = CarregarConfig();
        if (!config.BackupDiarioAutomatico) return;

        string hoje = DateTime.Today.ToString("yyyy-MM-dd");
        if (string.Equals(config.UltimaDataBackup, hoje, StringComparison.Ordinal))
            return;

        ExecutarBackup("diario");
    }
}
