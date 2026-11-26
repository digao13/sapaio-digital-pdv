using System;
using System.IO;
using System.Windows;

namespace SapaioDigital.App.Helpers;

public static class ThemeService
{
    private static string ThemeFilePath => Path.Combine(AppPaths.DataRoot, "theme.txt");

    public static bool IsDark { get; private set; }

    public static void LoadSavedTheme()
    {
        try
        {
            AppPaths.EnsureDirectories();
            if (File.Exists(ThemeFilePath))
            {
                string val = File.ReadAllText(ThemeFilePath).Trim();
                ApplyTheme(string.Equals(val, "Dark", StringComparison.OrdinalIgnoreCase), save: false);
                return;
            }
        }
        catch { }

        ApplyTheme(false, save: false);
    }

    public static void ToggleTheme() => ApplyTheme(!IsDark, save: true);

    public static void ApplyTheme(bool dark, bool save = true)
    {
        IsDark = dark;
        var merged = Application.Current?.Resources.MergedDictionaries;
        if (merged != null)
        {
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                if (merged[i].Source?.OriginalString.Contains("Themes/") == true)
                    merged.RemoveAt(i);
            }

            string file = dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            merged.Add(new ResourceDictionary { Source = new Uri(file, UriKind.Relative) });
        }

        if (save)
        {
            try
            {
                AppPaths.EnsureDirectories();
                File.WriteAllText(ThemeFilePath, dark ? "Dark" : "Light");
            }
            catch { }
        }
    }
}
