using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using MetroKafeMklink.Services;

namespace MetroKafeMklink.Views;

public partial class SettingsView : UserControl, IPage
{
    public SettingsView()
    {
        InitializeComponent();
        LoadFromConfig();
    }

    public void OnShown() => LoadFromConfig();

    private void LoadFromConfig()
    {
        var s = AppState.Config.Settings;
        CenterBox.Text = s.CenterPath;
        CenterBox_TextChanged(CenterBox, null!);
        ProcessesBox.Text = s.CloseProcesses;
        RepoBox.Text = s.GitHubRepo;
        CheckUpdatesBox.IsChecked = s.CheckUpdatesOnStart;
        TokenBox.Password = "";
        UpdateTokenInfo();

        var exe = Environment.ProcessPath ?? "MetroKafeMklink.exe";
        CommandBox.Text = $"\"{exe}\" /bagla";
        ConfigBox.Text = AppState.ConfigPath;
    }

    private void UpdateTokenInfo()
    {
        var hasToken = Secret.Unprotect(AppState.Config.Settings.EncryptedToken).Length > 0;
        TokenInfo.Text = hasToken ? "✓ Kayıtlı token var. Değiştirmek için yenisini yazın." : "Token yok; açık depo için gerekmez.";
        TokenInfo.Foreground = (System.Windows.Media.Brush)FindResource(hasToken ? "SuccessBrush" : "FaintTextBrush");
        ClearTokenButton.Visibility = hasToken ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CenterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var raw = CenterBox.Text.Trim();
        if (raw.StartsWith(@"\\")) { CenterInfo.Text = "⚠ Ağ yolu junction ile kullanılamaz."; return; }
        string p;
        try { p = AppState.ResolveCenter(raw); }
        catch { CenterInfo.Text = "⚠ Geçersiz yol."; return; }
        var root = Path.GetPathRoot(p);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) { CenterInfo.Text = "⚠ Sürücü bulunamadı: " + p; return; }
        try
        {
            var drive = new DriveInfo(root);
            var exists = Directory.Exists(p) ? "klasör var" : "klasör ilk işlemde oluşturulacak";
            var source = raw.Length == 0 ? "varsayılan: exe'nin klasörü · " : "";
            CenterInfo.Text = $"= {p}\n{source}{drive.Name} · {drive.AvailableFreeSpace / 1073741824.0:N0} GB boş · {exists}";
        }
        catch { CenterInfo.Text = "= " + p; }
    }

    private void BrowseCenter_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Merkez klasörü seç" };
        var current = AppState.ResolveCenter(CenterBox.Text);
        if (Directory.Exists(current)) dlg.InitialDirectory = current;
        if (dlg.ShowDialog() != true) return;

        // Exe klasorunun icindeyse goreli yaz ki exe ile birlikte tasinabilsin
        var picked = dlg.FolderName.TrimEnd('\\');
        var exe = AppState.ExeFolder;
        if (picked.Equals(exe, StringComparison.OrdinalIgnoreCase)) CenterBox.Text = "";
        else if (picked.StartsWith(exe + "\\", StringComparison.OrdinalIgnoreCase)) CenterBox.Text = "." + picked[exe.Length..];
        else CenterBox.Text = picked;
    }

    private void OpenCenter_Click(object sender, RoutedEventArgs e) => ProcessRunner.OpenInExplorer(AppState.ResolveCenter(CenterBox.Text));

    private void OpenConfig_Click(object sender, RoutedEventArgs e) => ProcessRunner.OpenInExplorer(AppState.ConfigPath);

    private void CopyCommand_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(CommandBox.Text);
        AppState.AddLog("Komut panoya kopyalandı");
    }

    private void ClearToken_Click(object sender, RoutedEventArgs e)
    {
        AppState.Config.Settings.EncryptedToken = "";
        AppState.Save();
        AppState.AddLog("GitHub token silindi", LogKind.Warning);
        UpdateTokenInfo();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var center = CenterBox.Text.Trim();
        if (center is @".\" or ".") center = "";
        else if (center.Length > 3) center = center.TrimEnd('\\');
        string resolved;
        try { resolved = AppState.ResolveCenter(center); }
        catch { resolved = ""; }
        if (center.StartsWith(@"\\") || resolved.Length == 0 || !Directory.Exists(Path.GetPathRoot(resolved)))
        {
            await MainWindow.Instance.InfoAsync("Kaydedilemedi",
                "Merkez klasör yerel bir diskte olmalı. Boş bırakırsanız exe'nin bulunduğu klasör kullanılır; .\\Merkez gibi göreli ya da D:\\Merkez gibi tam yol da yazabilirsiniz.");
            return;
        }
        var repo = RepoBox.Text.Trim().Trim('/');
        if (repo.Length > 0 && repo.Count(c => c == '/') != 1)
        {
            await MainWindow.Instance.InfoAsync("Kaydedilemedi", "Depo 'sahip/ad' biçiminde olmalı (örn. cetoxQ/MetroKafeMklink).");
            return;
        }

        var s = AppState.Config.Settings;
        var centerChanged = !string.Equals(AppState.CenterPath, resolved, StringComparison.OrdinalIgnoreCase);
        s.CenterPath = center;
        s.CloseProcesses = ProcessesBox.Text.Trim();
        s.GitHubRepo = repo;
        s.CheckUpdatesOnStart = CheckUpdatesBox.IsChecked == true;
        if (TokenBox.Password.Length > 0) s.EncryptedToken = Secret.Protect(TokenBox.Password.Trim());

        AppState.Save();
        AppState.AddLog("Ayarlar kaydedildi", LogKind.Success);
        TokenBox.Password = "";
        UpdateTokenInfo();
        if (centerChanged) MainWindow.Instance.RefreshAll();

        var flash = new DoubleAnimationUsingKeyFrames();
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));
        flash.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2300))));
        SavedText.BeginAnimation(OpacityProperty, flash);
    }
}
