using System;
using System.IO;

namespace SapaioDigital.App.Helpers;

/// <summary>
/// Centraliza os caminhos persistentes fora da pasta de instalação para garantir
/// que atualizações via GitHub nunca sobrescrevam o banco de dados ou a logo da loja.
/// </summary>
public static class AppPaths
{
    public static string DataRoot { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SapaioDigital");

    public static string DatabasePath => Path.Combine(DataRoot, "sapaio_digital.db");
    public static string BackupsDir => Path.Combine(DataRoot, "Backups");
    public static string LogosDir => Path.Combine(DataRoot, "Logos");
    public static string OsPdfDir => Path.Combine(DataRoot, "OrdensServico");

    public static void OverrideDataRootForTests(string customPath)
    {
        DataRoot = customPath;
        EnsureDirectories();
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(LogosDir);
        Directory.CreateDirectory(OsPdfDir);
    }
}