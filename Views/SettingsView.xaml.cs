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
        TokenInfo.Text = hasToken ? "✓ Kayıtlı token var. Değiştirmek için yenisini yazın." : "Kayıtlı token yok.";
        TokenInfo.Foreground = (System.Windows.Media.Brush)FindResource(hasToken ? "SuccessBrush" : "FaintTextBrush");
        ClearTokenButton.Visibility = hasToken ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CenterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var p = Environment.ExpandEnvironmentVariables(CenterBox.Text.Trim());
        if (p.Length == 0) { CenterInfo.Text = ""; return; }
        if (p.StartsWith(@"\\")) { CenterInfo.Text = "⚠ Ağ yolu junction ile kullanılamaz."; return; }
        var root = Path.GetPathRoot(p);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) { CenterInfo.Text = "⚠ Sürücü bulunamadı."; return; }
        try
        {
            var drive = new DriveInfo(root);
            var exists = Directory.Exists(p) ? "klasör var" : "klasör ilk işlemde oluşturulacak";
            CenterInfo.Text = $"{drive.Name} · {drive.AvailableFreeSpace / 1073741824.0:N0} GB boş · {exists}";
        }
        catch { CenterInfo.Text = ""; }
    }

    private void BrowseCenter_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Merkez klasörü seç" };
        if (Directory.Exists(CenterBox.Text)) dlg.InitialDirectory = CenterBox.Text;
        if (dlg.ShowDialog() == true) CenterBox.Text = dlg.FolderName;
    }

    private void OpenCenter_Click(object sender, RoutedEventArgs e) => ProcessRunner.OpenInExplorer(CenterBox.Text.Trim());

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
        var center = CenterBox.Text.Trim().TrimEnd('\\');
        if (center.StartsWith(@"\\") || !Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(center)))
        {
            await MainWindow.Instance.InfoAsync("Kaydedilemedi", "Merkez klasör yerel bir diskte tam bir yol olmalı (örn. D:\\MetroKafeMerkez).");
            return;
        }
        var repo = RepoBox.Text.Trim().Trim('/');
        if (repo.Length > 0 && repo.Count(c => c == '/') != 1)
        {
            await MainWindow.Instance.InfoAsync("Kaydedilemedi", "Depo 'sahip/ad' biçiminde olmalı (örn. cetoxQ/MetroKafeMklink).");
            return;
        }

        var s = AppState.Config.Settings;
        var centerChanged = !string.Equals(s.CenterPath.TrimEnd('\\'), center, StringComparison.OrdinalIgnoreCase);
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
