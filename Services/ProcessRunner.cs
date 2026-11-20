using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MetroKafeMklink.Services;

public static class ProcessRunner
{
    public record Result(int ExitCode, string Output);

    private static Encoding OemEncoding
    {
        get
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { return Encoding.UTF8; }
        }
    }

    /// <summary>Konsol programini gizli calistirir, cikti ve cikis kodunu dondurur.</summary>
    public static Result Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = OemEncoding,
            StandardErrorEncoding = OemEncoding,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException(exe + " başlatılamadı");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        return new Result(p.ExitCode, (stdout.Result + stderr.Result).Trim());
    }

    /// <summary>Virgulle ayrilmis listedeki programlari kapatir.</summary>
    public static void KillProcesses(string list)
    {
        var names = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(n => n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n);
        foreach (var name in names)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    p.Kill(true);
                    p.WaitForExit(5000);
                    AppState.AddLog("Kapatıldı: " + name, LogKind.Warning);
                }
                catch (Exception ex)
                {
                    AppState.AddLog($"{name} kapatılamadı: {ex.Message}", LogKind.Error);
                }
                finally { p.Dispose(); }
            }
        }
    }

    public static void OpenInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        else
        {
            var parent = Path.GetDirectoryName(path.TrimEnd('\\'));
            if (parent != null && Directory.Exists(parent)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{parent}\"") { UseShellExecute = true });
        }
    }
}
