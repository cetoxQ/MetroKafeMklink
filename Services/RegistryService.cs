using System.Diagnostics;
using Microsoft.Win32;
using MetroKafeMklink.Models;

namespace MetroKafeMklink.Services;

public static class RegistryService
{
    private static readonly Dictionary<string, (RegistryHive Hive, string FullName)> Hives = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HKLM"] = (RegistryHive.LocalMachine, "HKEY_LOCAL_MACHINE"),
        ["HKEY_LOCAL_MACHINE"] = (RegistryHive.LocalMachine, "HKEY_LOCAL_MACHINE"),
        ["HKCU"] = (RegistryHive.CurrentUser, "HKEY_CURRENT_USER"),
        ["HKEY_CURRENT_USER"] = (RegistryHive.CurrentUser, "HKEY_CURRENT_USER"),
        ["HKCR"] = (RegistryHive.ClassesRoot, "HKEY_CLASSES_ROOT"),
        ["HKEY_CLASSES_ROOT"] = (RegistryHive.ClassesRoot, "HKEY_CLASSES_ROOT"),
        ["HKU"] = (RegistryHive.Users, "HKEY_USERS"),
        ["HKEY_USERS"] = (RegistryHive.Users, "HKEY_USERS"),
    };

    public static string RegistryFolder => Path.Combine(AppState.Expand("{MERKEZ}"), "Registry");

    public static bool TryParse(string key, out RegistryHive hive, out string subKey, out string fullHive)
    {
        hive = default; subKey = ""; fullHive = "";
        var k = (key ?? "").Trim().Trim('\\');
        if (k.StartsWith(@"Computer\", StringComparison.OrdinalIgnoreCase)) k = k[9..];
        var i = k.IndexOf('\\');
        var head = i < 0 ? k : k[..i];
        if (!Hives.TryGetValue(head, out var h)) return false;
        hive = h.Hive; fullHive = h.FullName;
        subKey = i < 0 ? "" : k[(i + 1)..];
        return true;
    }

    public static bool KeyExists(string key)
    {
        if (!TryParse(key, out var hive, out var sub, out _)) return false;
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        if (sub.Length == 0) return true;
        using var k = root.OpenSubKey(sub);
        return k != null;
    }

    public static void Refresh(RegEntry e)
    {
        var file = Path.Combine(RegistryFolder, e.FileName);
        e.ResolvedFile = file;
        e.KeyExists = KeyExists(e.Key);
        var fi = new FileInfo(file);
        e.FileExists = fi.Exists;
        e.FileInfo = fi.Exists ? $"{fi.LastWriteTime:dd.MM.yyyy HH:mm} · {Math.Max(1, fi.Length / 1024)} KB" : "Dosya yok";
    }

    /// <summary>Anahtari merkezdeki .reg dosyasina yazar.</summary>
    public static void Export(RegEntry e)
    {
        Directory.CreateDirectory(RegistryFolder);
        var r = ProcessRunner.Run("reg.exe", $"export \"{e.Key}\" \"{e.ResolvedFile}\" /y");
        if (r.ExitCode != 0) throw new IOException(r.Output);
    }

    /// <summary>Merkezdeki .reg dosyasini bu bilgisayara uygular.</summary>
    public static void Import(RegEntry e)
    {
        if (!File.Exists(e.ResolvedFile)) throw new FileNotFoundException(".reg dosyası yok: " + e.ResolvedFile);
        var r = ProcessRunner.Run("reg.exe", $"import \"{e.ResolvedFile}\"");
        if (r.ExitCode != 0) throw new IOException(r.Output);
    }

    public static void OpenInRegedit(string key)
    {
        if (!TryParse(key, out _, out var sub, out var fullHive)) throw new ArgumentException("Anahtar tanınmadı: " + key);

        using var applet = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
        // Kok adi Windows diline gore degisir ("Bilgisayar" / "Computer"); mevcut degerden al
        var prefix = "Computer";
        if (applet.GetValue("LastKey") is string last && last.Contains('\\'))
        {
            var head = last[..last.IndexOf('\\')];
            if (!head.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase)) prefix = head;
        }
        applet.SetValue("LastKey", prefix + "\\" + fullHive + (sub.Length > 0 ? "\\" + sub : ""));
        Process.Start(new ProcessStartInfo("regedit.exe", "-m") { UseShellExecute = true });
    }
}
