using System.Text.Json;

namespace Visary.App.Shell;

/// <summary>Per user settings stored in %LOCALAPPDATA%\VisaryPRSNT\settings.json.</summary>
public sealed class Settings
{
    public string AppTheme { get; set; } = "powerpoint";
    public List<string> RecentFiles { get; set; } = [];
    public bool ShowNotes { get; set; } = true;
    public bool PresenterView { get; set; } = true;
    /// <summary>Use the Mica material behind the window chrome (Windows 11).</summary>
    public bool Mica { get; set; } = true;
    /// <summary>Preview a transition on the canvas while hovering its tile.</summary>
    public bool LivePreview { get; set; } = true;
    /// <summary>Save every change automatically once the file has a location, like Office AutoSave.</summary>
    public bool AutoSave { get; set; }
    public bool SeenTour { get; set; }

    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisaryPRSNT");
    static string FilePath => Path.Combine(Folder, "settings.json");

    public static Settings Current { get; } = Load();

    static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception ex)
        {
            Log.Error("Reading settings", ex);
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log.Error("Saving settings", ex);
        }
    }

    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > 12) RecentFiles.RemoveRange(12, RecentFiles.Count - 12);
        Save();
    }
}

public static class Log
{
    static readonly object Gate = new();
    public static string FilePath { get; } = Path.Combine(Settings.Folder, "visary.log");
    public static readonly List<string> Errors = [];

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string context, Exception? ex)
    {
        var msg = $"{context}: {ex}";
        lock (Gate) Errors.Add(msg);
        Write("ERROR", msg);
    }

    static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Settings.Folder);
                File.AppendAllText(FilePath, $"{DateTime.Now:O} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
        }
        System.Diagnostics.Debug.WriteLine($"{level} {message}");
    }
}
