using System.Windows;
using System.Windows.Controls;
using MetroKafeMklink.Services;

namespace MetroKafeMklink.Views;

public partial class UpdateView : UserControl, IPage
{
    private ReleaseInfo? _latest;
    private bool _busy;

    public UpdateView()
    {
        InitializeComponent();
        CurrentText.Text = "v" + AppState.VersionText;
    }

    public void OnShown()
    {
        if (_latest == null && !_busy) _ = CheckAsync();
    }

    /// <summary>Acilistaki sessiz kontrolun sonucunu sayfaya isler.</summary>
    public void SetLatest(ReleaseInfo info)
    {
        _latest = info;
        Show(info);
    }

    private void Check_Click(object sender, RoutedEventArgs e) => _ = CheckAsync();

    private async Task CheckAsync()
    {
        if (_busy) return;
        _busy = true;
        CheckButton.IsEnabled = false;
        InstallButton.IsEnabled = false;
        SetStatus("Denetleniyor…", "SubTextBrush", "");
        try
        {
            _latest = await UpdateService.GetLatestAsync();
            Show(_latest);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, "DangerBrush", "");
            AppState.AddLog("Güncelleme denetimi: " + ex.Message, LogKind.Error);
        }
        finally
        {
            CheckButton.IsEnabled = true;
            _busy = false;
        }
    }

    private void Show(ReleaseInfo info)
    {
        LatestText.Text = info.Tag;
        LatestDate.Text = info.Published is { } d ? d.ToString("dd MMMM yyyy, HH:mm") : "";
        NotesTitle.Text = info.Title;
        NotesText.Text = string.IsNullOrWhiteSpace(info.Notes) ? "Bu sürüm için not yazılmamış." : info.Notes;

        if (info.IsNewer && info.AssetUrl != null)
        {
            SetStatus($"Yeni sürüm hazır: {info.Tag}", "AccentBrush", "");
            InstallButton.IsEnabled = true;
            InstallText.Text = $"{info.Tag} indir ve kur";
        }
        else if (info.IsNewer)
        {
            SetStatus("Yeni sürüm var ama içinde .exe dosyası yok.", "WarningBrush", "");
        }
        else
        {
            SetStatus("Güncelsiniz.", "SuccessBrush", "");
            InstallButton.IsEnabled = false;
            InstallText.Text = "İndir ve kur";
        }
        MainWindow.Instance?.SetUpdateAvailable(info.IsNewer && info.AssetUrl != null);
    }

    private void SetStatus(string text, string brushKey, string icon)
    {
        var brush = (System.Windows.Media.SolidColorBrush)FindResource(brushKey);
        StatusText.Text = text;
        StatusText.Foreground = brush;
        StatusIcon.Text = icon;
        StatusIcon.Foreground = brush;
        var c = brush.Color;
        StatusBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x1A, c.R, c.G, c.B));
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_latest == null || _busy) return;
        if (!await MainWindow.Instance.ConfirmAsync("Güncelle",
                $"{_latest.Tag} indirilecek. İndirme bitince uygulama kapanıp yeni sürümle yeniden açılacak.", "İndir ve kur")) return;

        _busy = true;
        CheckButton.IsEnabled = false;
        InstallButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        var progress = new Progress<double>(p =>
        {
            Progress.Value = p;
            ProgressText.Text = $"%{p * 100:N0} indirildi";
        });
        try
        {
            var file = await UpdateService.DownloadAsync(_latest, progress);
            AppState.AddLog($"{_latest.Tag} indirildi, kuruluyor", LogKind.Success);
            ProgressText.Text = "Kuruluyor, uygulama yeniden başlatılacak…";
            await Task.Delay(600);
            UpdateService.InstallAndRestart(file);
        }
        catch (Exception ex)
        {
            SetStatus("İndirilemedi: " + ex.Message, "DangerBrush", "");
            AppState.AddLog("Güncelleme indirilemedi: " + ex.Message, LogKind.Error);
            ProgressPanel.Visibility = Visibility.Collapsed;
            CheckButton.IsEnabled = true;
            InstallButton.IsEnabled = true;
            _busy = false;
        }
    }
}
