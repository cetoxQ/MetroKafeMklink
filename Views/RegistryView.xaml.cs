using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MetroKafeMklink.Models;
using MetroKafeMklink.Services;

namespace MetroKafeMklink.Views;

public partial class RegistryView : UserControl, IPage
{
    private bool _busy;

    public RegistryView()
    {
        InitializeComponent();
        List.ItemsSource = AppState.Config.Registry;
        UpdateSelectionText();
    }

    public void OnShown() => _ = RefreshAsync();

    private List<RegEntry> Targets =>
        (List.SelectedItems.Count > 0 ? List.SelectedItems.Cast<RegEntry>() : AppState.Config.Registry.Where(r => r.Enabled)).ToList();

    public async Task RefreshAsync()
    {
        FolderRun.Text = RegistryService.RegistryFolder;
        var items = AppState.Config.Registry.ToList();
        await Task.Run(() => items.ForEach(RegistryService.Refresh));
        EmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        var n = List.SelectedItems.Count;
        SelectionText.Text = n == 0 ? "Seçim yok: tüm etkin kayıtlar" : $"{n} kayıt seçili";
    }

    // ---- Kayit islemleri ----

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var entry = new RegEntry();
        if (!await MainWindow.Instance.ShowDialogAsync("Yeni registry anahtarı", new RegEditor(entry), "Ekle")) return;
        AppState.Config.Registry.Add(entry);
        Save($"Eklendi: {entry.Name}");
        List.SelectedItem = entry;
    }

    private async void Edit_Click(object sender, RoutedEventArgs e) => await EditAsync(List.SelectedItem as RegEntry);

    private async void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is RegEntry entry) await EditAsync(entry);
    }

    private async Task EditAsync(RegEntry? entry)
    {
        if (entry == null) { await MainWindow.Instance.InfoAsync("Düzenle", "Önce listeden bir kayıt seçin."); return; }
        var copy = entry.Clone();
        if (!await MainWindow.Instance.ShowDialogAsync("Registry anahtarını düzenle", new RegEditor(copy), "Kaydet")) return;
        entry.CopyFrom(copy);
        Save($"Güncellendi: {entry.Name}");
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var items = List.SelectedItems.Cast<RegEntry>().ToList();
        if (items.Count == 0) { await MainWindow.Instance.InfoAsync("Sil", "Önce listeden kayıt seçin."); return; }
        var names = string.Join("\n", items.Select(i => "• " + i.Name));
        if (!await MainWindow.Instance.ConfirmAsync("Kayıtları sil",
                $"{names}\n\nYalnızca listeden silinir. Registry'ye ve .reg dosyalarına dokunulmaz.", "Sil", danger: true)) return;
        foreach (var i in items) AppState.Config.Registry.Remove(i);
        Save($"{items.Count} registry kaydı listeden silindi");
    }

    private void Save(string log)
    {
        AppState.Save();
        AppState.AddLog(log);
        _ = RefreshAsync();
    }

    private async void Regedit_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not RegEntry entry) { await MainWindow.Instance.InfoAsync("Regedit", "Önce listeden bir kayıt seçin."); return; }
        try { RegistryService.OpenInRegedit(entry.Key); }
        catch (Exception ex) { AppState.AddLog(ex.Message, LogKind.Error); }
    }

    // ---- Disa / ice aktarma ----

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var items = Targets;
        if (items.Count == 0 || _busy) return;
        var overwrite = items.Count(i => i.FileExists);
        var msg = $"{items.Count} anahtar şu klasöre yazılacak:\n{RegistryService.RegistryFolder}";
        if (overwrite > 0) msg += $"\n\n{overwrite} dosya zaten var, üzerine yazılacak.";
        if (!await MainWindow.Instance.ConfirmAsync("Dışa aktar", msg, "Dışa aktar")) return;

        await RunBusyAsync("Dışa aktarılıyor…", () =>
        {
            foreach (var i in items)
            {
                try
                {
                    if (!RegistryService.KeyExists(i.Key)) throw new InvalidOperationException("anahtar bu bilgisayarda yok");
                    RegistryService.Export(i);
                    AppState.AddLog($"{i.Name}: dışa aktarıldı → {i.ResolvedFile}", LogKind.Success);
                }
                catch (Exception ex) { AppState.AddLog($"{i.Name}: {ex.Message}", LogKind.Error); }
            }
        });
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var items = Targets;
        if (items.Count == 0 || _busy) return;
        var missing = items.Where(i => !i.FileExists).ToList();
        var ready = items.Except(missing).ToList();
        if (ready.Count == 0) { await MainWindow.Instance.InfoAsync("İçe aktar", "Merkezde hazır .reg dosyası yok. Önce ana makinede 'Dışa aktar' yapın."); return; }

        var msg = string.Join("\n", ready.Select(i => $"• {i.Name}  ({i.Key})")) + "\n\nBu anahtarlar bu bilgisayarın registry'sine yazılacak.";
        if (missing.Count > 0) msg += $"\n\n{missing.Count} kaydın dosyası olmadığı için atlanacak.";
        if (!await MainWindow.Instance.ConfirmAsync("İçe aktar", msg, "İçe aktar")) return;

        await RunBusyAsync("İçe aktarılıyor…", () =>
        {
            foreach (var i in ready)
            {
                try
                {
                    RegistryService.Import(i);
                    AppState.AddLog($"{i.Name}: içe aktarıldı", LogKind.Success);
                }
                catch (Exception ex) { AppState.AddLog($"{i.Name}: {ex.Message}", LogKind.Error); }
            }
        });
    }

    private async Task RunBusyAsync(string text, Action work)
    {
        _busy = true;
        BusyText.Text = text;
        BusyPanel.Visibility = Visibility.Visible;
        try
        {
            await Task.Run(work);
            await RefreshAsync();
        }
        finally
        {
            BusyPanel.Visibility = Visibility.Collapsed;
            _busy = false;
        }
    }
}
