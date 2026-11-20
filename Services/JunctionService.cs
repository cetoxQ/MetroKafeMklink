using MetroKafeMklink.Models;

namespace MetroKafeMklink.Services;

public enum LinkAction { None, Create, MoveAndLink, BackupAndLink, Relink, Error }

public static class JunctionService
{
    /// <summary>Kaydin diskteki durumunu okur ve kayda yazar.</summary>
    public static void Refresh(LinkEntry e)
    {
        var link = AppState.Expand(e.LinkPath);
        var target = AppState.Expand(e.TargetPath);
        var (state, detail) = Check(link, target);
        e.ResolvedLink = link;
        e.ResolvedTarget = target;
        e.Detail = detail;
        e.State = state;
    }

    public static (LinkState State, string Detail) Check(string link, string target)
    {
        if (link.Length == 0 || target.Length == 0) return (LinkState.Unknown, "Yol boş");

        var targetExists = Directory.Exists(target);
        FileAttributes attr;
        try
        {
            attr = File.GetAttributes(link);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return targetExists
                ? (LinkState.Missing, "Hedef hazır, bağlantı kurulabilir")
                : (LinkState.TargetMissing, "Ne bağlantı ne de hedef klasör var");
        }

        if (attr.HasFlag(FileAttributes.ReparsePoint))
        {
            var current = GetLinkTarget(link);
            if (current != null && !SamePath(current, target))
                return (LinkState.WrongTarget, "Şu an gösterdiği yer: " + current);
            return targetExists ? (LinkState.Linked, "Bağlı") : (LinkState.TargetMissing, "Bağlantı var ama hedef klasör yok");
        }

        if (!attr.HasFlag(FileAttributes.Directory)) return (LinkState.Unknown, "Bu yolda klasör değil dosya var");

        return targetExists
            ? (LinkState.RealFolder, "Merkezde de var; buradaki klasör yedeklenip bağlanır")
            : (LinkState.RealFolder, "Merkeze taşınıp bağlanabilir");
    }

    public static LinkAction PlanAction(LinkEntry e) => e.State switch
    {
        LinkState.Linked => LinkAction.None,
        LinkState.Missing => Directory.Exists(e.ResolvedTarget) ? LinkAction.Create : LinkAction.Error,
        LinkState.RealFolder => Directory.Exists(e.ResolvedTarget) ? LinkAction.BackupAndLink : LinkAction.MoveAndLink,
        LinkState.WrongTarget => Directory.Exists(e.ResolvedTarget) ? LinkAction.Relink : LinkAction.Error,
        _ => LinkAction.Error,
    };

    public static string Describe(LinkAction a) => a switch
    {
        LinkAction.Create => "Junction oluşturulacak",
        LinkAction.MoveAndLink => "Klasör merkeze taşınacak, yerine junction konacak",
        LinkAction.BackupAndLink => "Buradaki klasör yedeklenecek (.yedek_), yerine junction konacak",
        LinkAction.Relink => "Yanlış junction silinip doğrusu oluşturulacak",
        LinkAction.Error => "Yapılamaz: hedef klasör yok",
        _ => "Değişiklik yok",
    };

    /// <summary>Islem kaydi icin gecmis zamanli anlatim.</summary>
    public static string DescribeDone(LinkAction a) => a switch
    {
        LinkAction.Create => "junction oluşturuldu",
        LinkAction.MoveAndLink => "merkeze taşındı ve bağlandı",
        LinkAction.BackupAndLink => "eski klasör yedeklendi ve bağlandı",
        LinkAction.Relink => "yanlış junction düzeltildi",
        _ => Describe(a),
    };

    public static void Apply(LinkEntry e, LinkAction action, IProgress<string>? progress = null)
    {
        var link = e.ResolvedLink;
        var target = e.ResolvedTarget;
        switch (action)
        {
            case LinkAction.Create:
                CreateJunction(link, target);
                break;
            case LinkAction.Relink:
                RemoveJunction(link);
                CreateJunction(link, target);
                break;
            case LinkAction.BackupAndLink:
                var backup = link.TrimEnd('\\') + ".yedek_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                Directory.Move(link, backup);
                AppState.AddLog("Yedeklendi: " + backup, LogKind.Warning);
                CreateJunction(link, target);
                break;
            case LinkAction.MoveAndLink:
                MoveDirectory(link, target, progress);
                CreateJunction(link, target);
                break;
            case LinkAction.None:
                break;
            default:
                throw new InvalidOperationException(Describe(action));
        }
    }

    public static bool IsJunction(string path)
    {
        try { return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint); }
        catch { return false; }
    }

    public static void CreateJunction(string link, string target)
    {
        var parent = Path.GetDirectoryName(link.TrimEnd('\\'));
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        var r = ProcessRunner.Run("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"");
        if (!IsJunction(link)) throw new IOException("mklink başarısız: " + r.Output);
    }

    /// <summary>Yalnizca junction'i siler; hedefteki veriye dokunmaz.</summary>
    public static void RemoveJunction(string link)
    {
        if (!IsJunction(link)) throw new IOException("Bu bir junction değil, silinmedi: " + link);
        Directory.Delete(link, false);
    }

    private static void MoveDirectory(string source, string destination, IProgress<string>? progress)
    {
        if (Directory.Exists(destination)) throw new IOException("Merkezde zaten var: " + destination);
        Directory.CreateDirectory(Path.GetDirectoryName(destination.TrimEnd('\\'))!);

        // Ayni surucude tasima yalnizca yeniden adlandirmadir, aninda biter
        if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report("Taşınıyor (aynı disk)…");
            Directory.Move(source, destination);
            return;
        }

        var files = new DirectoryInfo(source).GetFiles("*", SearchOption.AllDirectories);
        long total = Math.Max(1, files.Sum(f => f.Length));
        long done = 0;
        int lastPercent = -1;

        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        Directory.CreateDirectory(destination);

        foreach (var f in files)
        {
            File.Copy(f.FullName, Path.Combine(destination, Path.GetRelativePath(source, f.FullName)));
            done += f.Length;
            var percent = (int)(done * 100 / total);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report($"Kopyalanıyor %{percent}  ({done / 1048576:N0} / {total / 1048576:N0} MB)");
            }
        }

        progress?.Report("Eski klasör siliniyor…");
        Directory.Delete(source, true);
    }

    private static string? GetLinkTarget(string link)
    {
        try { return Normalize(new DirectoryInfo(link).LinkTarget); }
        catch { return null; }
    }

    private static string? Normalize(string? p)
    {
        if (p == null) return null;
        if (p.StartsWith(@"\??\")) p = p[4..];
        return p.TrimEnd('\\');
    }

    private static bool SamePath(string a, string b)
    {
        try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
    }
}
