using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MetroKafeMklink.Models;
using MetroKafeMklink.Services;

namespace MetroKafeMklink.Views;

public partial class LinksView : UserControl, IPage
{
    private const string AllGroups = "Tümü";
    private readonly ICollectionView _view;
    private string _group = AllGroups;
    private bool _busy;

    public LinksView()
    {
        InitializeComponent();
        _view = CollectionViewSource.GetDefaultView(AppState.Config.Links);
        _view.Filter = Filter;
        List.ItemsSource = _view;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.F5) { _ = RefreshAsync(); e.Handled = true; } };
        RebuildGroups();
        UpdateSelectionText();
        _ = RefreshAsync();
    }

    public void OnShown() => _ = RefreshAsync();

    private List<LinkEntry> Selected => List.SelectedItems.Cast<LinkEntry>().ToList();
    private List<LinkEntry> Visible => _view.Cast<LinkEntry>().ToList();

    // ---- Durum ----

    public async Task RefreshAsync()
    {
        var items = AppState.Config.Links.ToList();
        await Task.Run(() => items.ForEach(JunctionService.Refresh));
        UpdateStats();
    }

    private void UpdateStats()
    {
        var all = AppState.Config.Links.Where(l => l.Enabled).ToList();
        StatTotal.Text = AppState.Config.Links.Count.ToString();
        StatLinked.Text = all.Count(l => l.State == LinkState.Linked).ToString();
        StatPending.Text = all.Count(l => l.State is LinkState.Missing or LinkState.RealFolder).ToString();
        StatProblem.Text = all.Count(l => l.State is LinkState.WrongTarget or LinkState.TargetMissing or LinkState.Unknown).ToString();
        EmptyText.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- Filtre ----

    private bool Filter(object o)
    {
        if (o is not LinkEntry e) return false;
        if (_group != AllGroups && !string.Equals(e.Group, _group, StringComparison.OrdinalIgnoreCase)) return false;
        var q = SearchBox?.Text.Trim() ?? "";
        if (q.Length == 0) return true;
        return Contains(e.Name, q) || Contains(e.Group, q) || Contains(e.LinkPath, q) || Contains(e.TargetPath, q)
               || Contains(e.ResolvedLink, q) || Contains(e.ResolvedTarget, q);
    }

    private static bool Contains(string s, string q) => s.Contains(q, StringComparison.CurrentCultureIgnoreCase);

    private void RebuildGroups()
    {
        var groups = AppState.Config.Links.Select(l => l.Group).Where(g => !string.IsNullOrWhiteSpace(g))
                         .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(g => g).ToList();
        groups.Insert(0, AllGroups);
        if (!groups.Contains(_group, StringComparer.OrdinalIgnoreCase)) _group = AllGroups;
        GroupChips.ItemsSource = groups;
        GroupChips.SelectedItem = groups.First(g => string.Equals(g, _group, StringComparison.OrdinalIgnoreCase));
    }

    private void Group_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupChips.SelectedItem is not string g) return;
        _group = g;
        _view.Refresh();
        UpdateStats();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view.Refresh();
        UpdateStats();
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionText();

    private void UpdateSelectionText()
    {
        var n = List.SelectedItems.Count;
        SelectionText.Text = n == 0 ? "Seçim yok: görünen tüm etkin kayıtlar" : $"{n} kayıt seçili";
        SelectionText.ToolTip = n == 0 ? "Hiçbir kayıt seçili değilken işlemler listede görünen tüm etkin kayıtlara uygulanır" : null;
        LinkButtonText.Text = n == 0 ? "Tümünü bağla" : $"Seçilileri bağla ({n})";
    }

    // ---- Kayit islemleri ----

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var entry = new LinkEntry { Group = _group == AllGroups ? "" : _group };
        var editor = new LinkEditor(entry);
        if (!await MainWindow.Instance.ShowDialogAsync("Yeni bağlantı", editor, "Ekle")) return;
        AppState.Config.Links.Add(entry);
        SaveAndReload($"Eklendi: {entry.Name}");
        List.SelectedItem = entry;
        List.ScrollIntoView(entry);
    }

    private async void Edit_Click(object sender, RoutedEventArgs e) => await EditAsync(List.SelectedItem as LinkEntry);

    private async void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is LinkEntry entry) await EditAsync(entry);
    }

    private async Task EditAsync(LinkEntry? entry)
    {
        if (entry == null) { await MainWindow.Instance.InfoAsync("Düzenle", "Önce listeden bir kayıt seçin."); return; }
        var copy = entry.Clone();
        if (!await MainWindow.Instance.ShowDialogAsync("Bağlantıyı düzenle", new LinkEditor(copy), "Kaydet")) return;
        entry.CopyFrom(copy);
        SaveAndReload($"Güncellendi: {entry.Name}");
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var items = Selected;
        if (items.Count == 0) { await MainWindow.Instance.InfoAsync("Sil", "Önce listeden kayıt seçin."); return; }
        var names = string.Join("\n", items.Select(i => "• " + i.Name));
        if (!await MainWindow.Instance.ConfirmAsync("Kayıtları sil",
                $"{names}\n\nBu kayıtlar yalnızca listeden silinir. Diskteki klasörlere ve junction'lara dokunulmaz.", "Sil", danger: true)) return;
        foreach (var i in items) AppState.Config.Links.Remove(i);
        SaveAndReload($"{items.Count} kayıt listeden silindi");
    }

    private void SaveAndReload(string log)
    {
        AppState.Save();
        AppState.AddLog(log);
        RebuildGroups();
        _view.Refresh();
        _ = RefreshAsync();
    }

    private void List_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) Delete_Click(sender, e);
        else if (e.Key == Key.Enter) Edit_Click(sender, e);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is LinkEntry entry)
            ProcessRunner.OpenInExplorer(Directory.Exists(entry.ResolvedTarget) ? entry.ResolvedTarget : entry.ResolvedLink);
        else
            ProcessRunner.OpenInExplorer(AppState.CenterPath);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAsync();

    // ---- Diske uygulanan islemler ----

    private async void Link_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var targets = (Selected.Count > 0 ? Selected : Visible).Where(i => i.Enabled).ToList();
        if (targets.Count == 0) { await MainWindow.Instance.InfoAsync("Bağla", "Bağlanacak etkin kayıt yok."); return; }

        await Task.Run(() => targets.ForEach(JunctionService.Refresh));
        var plan = targets.Select(t => (Entry: t, Action: JunctionService.PlanAction(t)))
                          .Where(p => p.Action != LinkAction.None).ToList();
        UpdateStats();
        if (plan.Count == 0)
        {
            AppState.AddLog("Seçilen kayıtların hepsi zaten bağlı", LogKind.Success);
            await MainWindow.Instance.InfoAsync("Bağla", "Hepsi zaten bağlı, yapılacak bir şey yok.");
            return;
        }

        if (!await MainWindow.Instance.ShowDialogAsync("Yapılacak işlemler", BuildPlanView(plan), "Uygula")) return;

        var progress = new Progress<string>(s => BusyText.Text = s);
        await RunBusyAsync("Uygulanıyor…", () =>
        {
            ProcessRunner.KillProcesses(AppState.Config.Settings.CloseProcesses);
            foreach (var (entry, action) in plan)
            {
                if (action == LinkAction.Error)
                {
                    AppState.AddLog($"{entry.Name}: atlandı ({entry.Detail})", LogKind.Error);
                    continue;
                }
                try
                {
                    ((IProgress<string>)progress).Report(entry.Name + "…");
                    JunctionService.Apply(entry, action, progress);
                    AppState.AddLog($"{entry.Name}: {JunctionService.DescribeDone(action)}", LogKind.Success);
                }
                catch (Exception ex)
                {
                    AppState.AddLog($"{entry.Name}: {ex.Message}", LogKind.Error);
                }
            }
        });
    }

    private async void Unlink_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var items = Selected.Where(i => JunctionService.IsJunction(i.ResolvedLink)).ToList();
        if (items.Count == 0) { await MainWindow.Instance.InfoAsync("Bağlantıyı kaldır", "Seçili kayıtlar arasında kurulu bir junction yok."); return; }

        var names = string.Join("\n", items.Select(i => $"• {i.Name}   ({i.ResolvedLink})"));
        if (!await MainWindow.Instance.ConfirmAsync("Bağlantıyı kaldır",
                $"{names}\n\nYalnızca junction silinir. Veriler merkezde ({AppState.CenterPath}) olduğu gibi kalır.",
                "Kaldır", danger: true)) return;

        await RunBusyAsync("Kaldırılıyor…", () =>
        {
            foreach (var i in items)
            {
                try
                {
                    JunctionService.RemoveJunction(i.ResolvedLink);
                    AppState.AddLog($"{i.Name}: junction kaldırıldı", LogKind.Warning);
                }
                catch (Exception ex)
                {
                    AppState.AddLog($"{i.Name}: {ex.Message}", LogKind.Error);
                }
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

    private FrameworkElement BuildPlanView(List<(LinkEntry Entry, LinkAction Action)> plan)
    {
        var panel = new StackPanel();
        foreach (var (entry, action) in plan)
        {
            var color = action switch
            {
                LinkAction.Error => "DangerBrush",
                LinkAction.MoveAndLink or LinkAction.BackupAndLink => "WarningBrush",
                _ => "SuccessBrush",
            };
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(new TextBlock { Text = entry.Name, FontWeight = FontWeights.SemiBold });
            row.Children.Add(new TextBlock
            {
                Text = JunctionService.Describe(action),
                Foreground = (System.Windows.Media.Brush)FindResource(color),
                Margin = new Thickness(0, 2, 0, 0),
            });
            row.Children.Add(new TextBlock
            {
                Text = $"{entry.ResolvedLink}  →  {entry.ResolvedTarget}",
                Foreground = (System.Windows.Media.Brush)FindResource("FaintTextBrush"),
                FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
            });
            panel.Children.Add(row);
        }
        panel.Children.Add(new TextBlock
        {
            Text = "İşlemden önce Ayarlar'daki programlar kapatılır.",
            Style = (Style)FindResource("Hint"),
        });
        return panel;
    }
}
