using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using MetroKafeMklink.Models;

namespace MetroKafeMklink.Services;

public enum LogKind { Info, Success, Warning, Error }

public record LogLine(DateTime Time, string Text, LogKind Kind)
{
    public string TimeText => Time.ToString("HH:mm:ss");
}

/// <summary>Uygulama genelinde paylasilan yapilandirma ve islem kaydi.</summary>
public static class AppState
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppConfig Config { get; private set; } = new();
    public static ObservableCollection<LogLine> Log { get; } = new();

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "MetroKafeMklink.json");
    public static string LogPath => Path.Combine(AppContext.BaseDirectory, "MetroKafeMklink.log");
    public static Version Version => typeof(AppState).Assembly.GetName().Version ?? new Version(0, 0, 0);
    public static string VersionText => Version.ToString(3);

    public static void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                Config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), Json) ?? AppConfig.CreateDefault();
            }
            else
            {
                Config = AppConfig.CreateDefault();
                Save();
                AddLog("Varsayılan yapılandırma oluşturuldu: " + ConfigPath);
            }
        }
        catch (Exception ex)
        {
            Config = AppConfig.CreateDefault();
            AddLog("Yapılandırma okunamadı, varsayılan kullanılıyor: " + ex.Message, LogKind.Error);
        }
    }

    public static void Save()
    {
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Config, Json));
        File.Move(tmp, ConfigPath, true);
    }

    public static void AddLog(string text, LogKind kind = LogKind.Info)
    {
        var line = new LogLine(DateTime.Now, text, kind);
        void Add()
        {
            Log.Insert(0, line);
            while (Log.Count > 500) Log.RemoveAt(Log.Count - 1);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) Add();
        else dispatcher.BeginInvoke(Add);

        try { File.AppendAllText(LogPath, $"{line.Time:yyyy-MM-dd HH:mm:ss} [{kind}] {text}{Environment.NewLine}"); }
        catch { /* kayit dosyasi yazilamazsa arayuzdeki kayit yeterli */ }
    }

    public static string CenterPath => Config.Settings.CenterPath.Trim().TrimEnd('\\');

    /// <summary>{MERKEZ} ve %DEGISKEN% ifadelerini gercek yola cevirir.</summary>
    public static string Expand(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var center = CenterPath;
        var p = Regex.Replace(path.Trim(), @"\{MERKEZ\}", _ => center, RegexOptions.IgnoreCase);
        return Environment.ExpandEnvironmentVariables(p);
    }

    /// <summary>Gercek yolu tasinabilir hale getirir (ornek: C:\Users\x\AppData\Roaming\... -> %APPDATA%\...).</summary>
    public static string Compress(string path)
    {
        var known = new (string Value, string Token)[]
        {
            (CenterPath, "{MERKEZ}"),
            (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%APPDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "%PROGRAMDATA%"),
            (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%"),
        };
        foreach (var (value, token) in known.Where(k => !string.IsNullOrEmpty(k.Value)).OrderByDescending(k => k.Value.Length))
        {
            var v = value.TrimEnd('\\');
            if (path.Equals(v, StringComparison.OrdinalIgnoreCase)) return token;
            if (path.StartsWith(v + "\\", StringComparison.OrdinalIgnoreCase)) return token + path[v.Length..];
        }
        return path;
    }
}
