using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MetroKafeMklink.Models;
using MetroKafeMklink.Services;

namespace MetroKafeMklink.Views;

public partial class LinkEditor : UserControl, IDialogBody
{
    private readonly LinkEntry _entry;

    public LinkEditor(LinkEntry entry)
    {
        InitializeComponent();
        _entry = entry;
        DataContext = entry;
        UpdatePreviews();
    }

    private void Path_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreviews();

    private void UpdatePreviews()
    {
        if (LinkPreview == null || TargetPreview == null) return;
        LinkPreview.Text = Preview(LinkBox.Text);
        TargetPreview.Text = Preview(TargetBox.Text);
    }

    private static string Preview(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var p = AppState.Expand(raw);
        var state = Directory.Exists(p) ? (JunctionService.IsJunction(p) ? "junction var" : "klasör var") : "henüz yok";
        return $"= {p}   ({state})";
    }

    private void BrowseLink_Click(object sender, RoutedEventArgs e)
    {
        if (Browse(_entry.LinkPath) is { } p) _entry.LinkPath = AppState.Compress(p);
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (Browse(_entry.TargetPath) is { } p) _entry.TargetPath = AppState.Compress(p);
    }

    private static string? Browse(string current)
    {
        var dlg = new OpenFolderDialog { Title = "Klasör seç" };
        var expanded = AppState.Expand(current);
        if (Directory.Exists(expanded)) dlg.InitialDirectory = expanded;
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    private void Suggest_Click(object sender, RoutedEventArgs e)
    {
        var link = AppState.Expand(_entry.LinkPath).TrimEnd('\\');
        if (link.Length == 0) { ShowError("Önce bağlantı yolunu girin."); return; }

        // %APPDATA%\Riot Client -> {MERKEZ}\<Grup>\AppData\Riot Client gibi
        var compressed = AppState.Compress(link);
        string area = compressed.StartsWith("%APPDATA%", StringComparison.OrdinalIgnoreCase) ? "AppData"
                    : compressed.StartsWith("%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase) ? "LocalAppData"
                    : compressed.StartsWith("%PROGRAMDATA%", StringComparison.OrdinalIgnoreCase) ? "ProgramData"
                    : "";
        var group = string.IsNullOrWhiteSpace(_entry.Group) ? "Genel" : _entry.Group.Trim();
        var leaf = Path.GetFileName(link);
        _entry.TargetPath = string.Join("\\", new[] { "{MERKEZ}", group, area, leaf }.Where(s => s.Length > 0));
        ErrorText.Visibility = Visibility.Collapsed;
    }

    public bool Validate()
    {
        _entry.Name = _entry.Name.Trim();
        _entry.Group = _entry.Group.Trim();
        _entry.LinkPath = _entry.LinkPath.Trim();
        _entry.TargetPath = _entry.TargetPath.Trim();

        if (_entry.Name.Length == 0) return ShowError("Ad boş olamaz.");
        if (_entry.LinkPath.Length == 0 || _entry.TargetPath.Length == 0) return ShowError("Bağlantı ve hedef yolu dolu olmalı.");

        var link = AppState.Expand(_entry.LinkPath).TrimEnd('\\');
        var target = AppState.Expand(_entry.TargetPath).TrimEnd('\\');
        if (!Path.IsPathFullyQualified(link) || !Path.IsPathFullyQualified(target))
            return ShowError("Yollar tam olmalı (örn. C:\\… ya da %APPDATA%\\…).");
        if (string.Equals(link, target, StringComparison.OrdinalIgnoreCase)) return ShowError("Bağlantı ve hedef aynı olamaz.");
        if (Path.GetPathRoot(link)?.TrimEnd('\\').Equals(link, StringComparison.OrdinalIgnoreCase) == true)
            return ShowError("Sürücünün kendisi (örn. C:\\) bağlantı yapılamaz.");
        if (target.StartsWith(link + "\\", StringComparison.OrdinalIgnoreCase) || link.StartsWith(target + "\\", StringComparison.OrdinalIgnoreCase))
            return ShowError("Biri diğerinin içinde olamaz.");
        if (target.StartsWith(@"\\")) return ShowError("Junction ağ yolunu (\\\\sunucu\\…) gösteremez; yerel bir disk seçin.");
        return true;
    }

    private bool ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        return false;
    }
}
