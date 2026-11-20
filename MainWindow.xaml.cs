using System.Collections.Specialized;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MetroKafeMklink.Services;
using MetroKafeMklink.Views;

namespace MetroKafeMklink;

/// <summary>Sayfalar gorundugunde bilgilendirilir.</summary>
public interface IPage
{
    void OnShown();
}

/// <summary>Iletisim kutusu icerigi; Tamam'a basilinca dogrulanir.</summary>
public interface IDialogBody
{
    bool Validate();
}

public partial class MainWindow : Window
{
    public static MainWindow Instance { get; private set; } = null!;

    private TaskCompletionSource<bool>? _dialog;
    private bool _logOpen;

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();

        VersionText.Text = "Sürüm " + AppState.VersionText;
        var admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        AdminText.Text = admin ? "Yönetici olarak çalışıyor" : "Yönetici değil!";
        var brush = (System.Windows.Media.Brush)FindResource(admin ? "SuccessBrush" : "DangerBrush");
        AdminText.Foreground = brush;
        AdminIcon.Foreground = brush;

        LogList.ItemsSource = AppState.Log;
        AppState.Log.CollectionChanged += Log_CollectionChanged;
        ShowLastLog();

        NavLinks.IsChecked = true;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!AppState.Config.Settings.CheckUpdatesOnStart) return;
        try
        {
            var info = await UpdateService.GetLatestAsync();
            UpdatePage.SetLatest(info);
            if (info.IsNewer)
            {
                UpdateDot.Visibility = Visibility.Visible;
                AppState.AddLog($"Yeni sürüm var: {info.Tag}", LogKind.Warning);
            }
        }
        catch
        {
            // Acilistaki sessiz kontrol; hata Guncelleme sekmesinde elle denetlenince gorunur
        }
    }

    public void SetUpdateAvailable(bool available) =>
        UpdateDot.Visibility = available ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>{MERKEZ} gibi ayarlar degisince tum sayfalari yeniler.</summary>
    public void RefreshAll()
    {
        _ = LinksPage.RefreshAsync();
        _ = RegistryPage.RefreshAsync();
    }

    // ---- Gezinme ----

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PageHost == null) return;
        UIElement page = sender == NavRegistry ? RegistryPage
                       : sender == NavSettings ? SettingsPage
                       : sender == NavUpdate ? UpdatePage
                       : LinksPage;

        foreach (UIElement child in PageHost.Children)
            child.Visibility = child == page ? Visibility.Visible : Visibility.Collapsed;
        (page as IPage)?.OnShown();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        PageShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
    }

    // ---- Pencere ----

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // WindowChrome ile buyutulunce kenarlar ekran disina tasar
        Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Overlay.Visibility != Visibility.Visible) return;
        if (e.Key == Key.Escape && OverlayCancel.Visibility == Visibility.Visible) { CloseDialog(false); e.Handled = true; }
    }

    // ---- Islem kaydi ----

    private void Log_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ShowLastLog();

    private void ShowLastLog()
    {
        var last = AppState.Log.FirstOrDefault();
        LastLogText.Text = last == null ? "Henüz işlem yok" : $"{last.TimeText}  {last.Text}";
        LastLogText.Foreground = (System.Windows.Media.Brush)FindResource(last?.Kind switch
        {
            LogKind.Success => "SuccessBrush",
            LogKind.Warning => "WarningBrush",
            LogKind.Error => "DangerBrush",
            _ => "SubTextBrush",
        });
    }

    private void LogHeader_Click(object sender, MouseButtonEventArgs e)
    {
        _logOpen = !_logOpen;
        var anim = new DoubleAnimation(_logOpen ? 180 : 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        LogList.BeginAnimation(HeightProperty, anim);
        LogChevron.Text = _logOpen ? "" : "";
    }

    // ---- Iletisim kutusu ----

    public Task<bool> ShowDialogAsync(string title, object body, string okText = "Tamam", string? cancelText = "Vazgeç", bool danger = false)
    {
        _dialog?.TrySetResult(false);
        OverlayTitle.Text = title;
        OverlayBody.Content = body;
        OverlayOk.Content = okText;
        OverlayOk.Style = (Style)FindResource(danger ? "DangerButton" : "AccentButton");
        OverlayCancel.Visibility = cancelText == null ? Visibility.Collapsed : Visibility.Visible;
        if (cancelText != null) OverlayCancel.Content = cancelText;

        Overlay.Visibility = Visibility.Visible;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        OverlayScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });
        OverlayScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease });

        // Ilk metin kutusuna odaklan
        Dispatcher.BeginInvoke(() =>
        {
            if (body is DependencyObject d && FindFirst<TextBox>(d) is { } tb) { tb.Focus(); tb.SelectAll(); }
            else OverlayOk.Focus();
        }, System.Windows.Threading.DispatcherPriority.Input);

        _dialog = new TaskCompletionSource<bool>();
        return _dialog.Task;
    }

    public Task<bool> ConfirmAsync(string title, string message, string okText = "Evet", bool danger = false) =>
        ShowDialogAsync(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("SubTextBrush"), LineHeight = 20 },
                        okText, "Vazgeç", danger);

    public Task InfoAsync(string title, string message) =>
        ShowDialogAsync(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("SubTextBrush"), LineHeight = 20 },
                        "Tamam", null);

    private void OverlayOk_Click(object sender, RoutedEventArgs e)
    {
        if (OverlayBody.Content is IDialogBody body && !body.Validate()) return;
        CloseDialog(true);
    }

    private void OverlayCancel_Click(object sender, RoutedEventArgs e) => CloseDialog(false);

    private void CloseDialog(bool result)
    {
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120));
        anim.Completed += (_, _) =>
        {
            if (Overlay.Opacity == 0) Overlay.Visibility = Visibility.Collapsed;
        };
        Overlay.BeginAnimation(OpacityProperty, anim);
        var tcs = _dialog;
        _dialog = null;
        tcs?.TrySetResult(result);
    }

    private static T? FindFirst<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            if (FindFirst<T>(child) is { } found) return found;
        }
        return null;
    }
}
