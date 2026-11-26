using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SapaioDigital.App.Data;
using SapaioDigital.App.Helpers;

namespace SapaioDigital.App.Services;

public class BackupConfig
{
    public string PastaDestino { get; set; } = AppPaths.BackupsDir;
    public string Plataforma { get; set; } = "Local";
    public bool BackupDiarioAutomatico { get; set; } = true;
    public int ManterUltimosBackups { get; set; } = 30;
    public string UltimaDataBackup { get; set; } = "";
    public string UltimoArquivoBackup { get; set; } = "";
}

public record ProvedorNuvemStatus(string Nome, bool Disponivel, string? PastaSugerida, string StatusTexto);

public record BackupArquivoInfo(string NomeArquivo, DateTime DataCriacao, string TamanhoFormatado, string CaminhoCompleto);

/// <summary>
/// Gerencia backups locais e em nuvem via pastas sincronizadas (OneDrive, Dropbox, Google Drive)
/// com retenção automática, histórico e restauração em 1 clique.
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

    public static List<ProvedorNuvemStatus> ObterStatusProvedores()
    {
        var one = DetectarPastaOneDrive();
        var gdrive = DetectarPastaGoogleDrive();
        var drop = DetectarPastaDropbox();

        return new List<ProvedorNuvemStatus>
        {
            new("OneDrive", one != null, one, one != null ? $"Sincronizado ({one})" : "Não encontrado neste PC"),
            new("Google Drive", gdrive != null, gdrive, gdrive != null ? $"Sincronizado ({gdrive})" : "Instale o Google Drive para Desktop"),
            new("Dropbox", drop != null, drop, drop != null ? $"Sincronizado ({drop})" : "Não encontrado neste PC")
        };
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

        if (config.ManterUltimosBackups > 0)
        {
            LimparBackupsAntigos(pastaDestino, config.ManterUltimosBackups);
            LimparBackupsAntigos(AppPaths.BackupsDir, config.ManterUltimosBackups);
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

    public static List<BackupArquivoInfo> ListarBackupsDisponiveis()
    {
        var config = CarregarConfig();
        var pasta = string.IsNullOrWhiteSpace(config.PastaDestino) ? AppPaths.BackupsDir : config.PastaDestino;
        if (!Directory.Exists(pasta)) return new List<BackupArquivoInfo>();

        return new DirectoryInfo(pasta)
            .GetFiles("*.db")
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupArquivoInfo(
                f.Name,
                f.CreationTime,
                FormatarTamanho(f.Length),
                f.FullName))
            .ToList();
    }

    public static void RestaurarBackup(string caminhoBackup)
    {
        if (!File.Exists(caminhoBackup) || new FileInfo(caminhoBackup).Length == 0)
            throw new FileNotFoundException("Arquivo de backup inválido ou não encontrado.", caminhoBackup);

        // Faz backup preventivo do banco atual antes de restaurar
        DatabaseService.CriarBackupSeguro("pre_restauracao");

        SqliteConnection.ClearAllPools();
        File.Copy(caminhoBackup, AppPaths.DatabasePath, overwrite: true);
        DatabaseService.Initialize();

        var loja = DatabaseService.ObterLojaConfig();
        if (loja != null)
        {
            SessaoAtual.Loja = loja;
            SessaoAtual.NotificarLojaAtualizada();
        }
    }

    private static void LimparBackupsAntigos(string pasta, int manter)
    {
        try
        {
            if (!Directory.Exists(pasta)) return;
            var arquivos = new DirectoryInfo(pasta).GetFiles("sapaio_*.db").OrderByDescending(f => f.CreationTime).Skip(manter);
            foreach (var arq in arquivos)
            {
                arq.Delete();
            }
        }
        catch { }
    }

    private static string FormatarTamanho(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:N1} KB";
        return $"{bytes / (1024.0 * 1024.0):N2} MB";
    }
}
