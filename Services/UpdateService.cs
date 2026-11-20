using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace MetroKafeMklink.Services;

public record ReleaseInfo(string Tag, Version Version, string Title, string Notes, DateTime? Published,
                          string? AssetUrl, string? AssetName, long AssetSize)
{
    public bool IsNewer => Version > AppState.Version;
}

/// <summary>GitHub Releases uzerinden surum kontrolu ve kendini guncelleme.</summary>
public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MetroKafeMklink/" + AppState.VersionText);
        return http;
    }

    private static void AddAuth(HttpRequestMessage req)
    {
        var token = Secret.Unprotect(AppState.Config.Settings.EncryptedToken);
        if (token.Length > 0) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<ReleaseInfo> GetLatestAsync(CancellationToken ct = default)
    {
        var repo = AppState.Config.Settings.GitHubRepo.Trim().Trim('/');
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        AddAuth(req);

        using var res = await Http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Sürüm bulunamadı. Ayarlar'daki depo adını kontrol edin; depoda henüz yayın olmayabilir ya da depo gizliyse token gerekir.");
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("GitHub token geçersiz ya da süresi dolmuş. Açık depo için Ayarlar'dan token'ı silebilirsiniz.");
        if (res.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("GitHub istek sınırına takıldı. Biraz sonra tekrar deneyin ya da Ayarlar'a token girin.");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"GitHub yanıtı: {(int)res.StatusCode} {res.ReasonPhrase}");

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var r = doc.RootElement;
        var tag = r.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) version = new Version(0, 0, 0);

        string? assetUrl = null, assetName = null;
        long assetSize = 0;
        if (r.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                assetUrl = a.GetProperty("url").GetString();
                assetName = name;
                assetSize = a.GetProperty("size").GetInt64();
                break;
            }
        }

        DateTime? published = r.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetDateTime().ToLocalTime() : null;
        var title = r.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? tag : tag;
        var notes = r.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "";

        return new ReleaseInfo(tag, version, title, notes, published, assetUrl, assetName, assetSize);
    }

    public static async Task<string> DownloadAsync(ReleaseInfo info, IProgress<double> progress, CancellationToken ct = default)
    {
        if (info.AssetUrl == null) throw new InvalidOperationException("Bu sürümde indirilecek .exe dosyası yok.");

        using var req = new HttpRequestMessage(HttpMethod.Get, info.AssetUrl);
        req.Headers.Accept.ParseAdd("application/octet-stream");
        AddAuth(req);

        using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();

        var path = Path.Combine(Path.GetTempPath(), $"MetroKafeMklink_{info.Tag}.exe");
        var total = res.Content.Headers.ContentLength ?? info.AssetSize;
        await using (var src = await res.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(path))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0) progress.Report((double)read / total);
            }
        }
        return path;
    }

    /// <summary>Uygulama kapaninca yeni exe'yi eskisinin yerine koyup yeniden baslatir.</summary>
    public static void InstallAndRestart(string newExe)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Uygulama yolu bulunamadı.");
        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), "MetroKafeMklink_guncelle.cmd");

        var lines = new[]
        {
            "@echo off",
            ":bekle",
            $"tasklist /fi \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto bekle)",
            $"move /y \"{newExe}\" \"{exe}\" >nul",
            $"start \"\" \"{exe}\"",
            "del \"%~f0\"",
        };
        Encoding oem;
        try { oem = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
        catch { oem = Encoding.ASCII; }
        File.WriteAllText(script, string.Join("\r\n", lines) + "\r\n", oem);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
        Application.Current.Shutdown();
    }
}
