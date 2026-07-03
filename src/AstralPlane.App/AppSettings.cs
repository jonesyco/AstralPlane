using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;

namespace AstralPlane_App;

/// <summary>The user's chosen app theme.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Persisted user preferences. Stored as JSON under
/// <c>%LOCALAPPDATA%\AstralPlane\settings.json</c> because the app ships unpackaged
/// (<c>WindowsPackageType=None</c>), so <c>ApplicationData.Current</c> is unavailable.
/// Load/save fail soft: a missing or corrupt file yields defaults.
/// </summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AstralPlane", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch
        {
            // Corrupt or unreadable settings fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
        }
        catch
        {
            // Best-effort; a failed save must never crash the app.
        }
    }

    /// <summary>Maps the stored choice to the element-level theme lever (System = follow OS).</summary>
    public static ElementTheme ToElementTheme(AppTheme theme) => theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };
}
