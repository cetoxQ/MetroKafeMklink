using System.Text;
using System.Windows;
using MetroKafeMklink.Services;

namespace MetroKafeMklink;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // cmd/reg ciktilarini OEM kod sayfasiyla (857) okuyabilmek icin
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        base.OnStartup(e);

        if (e.Args.Any(a => a.TrimStart('/', '-').Equals("bagla", StringComparison.OrdinalIgnoreCase)))
        {
            Shutdown(CliRunner.Run(e.Args));
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            AppState.AddLog("Beklenmeyen hata: " + ex.Exception.Message, LogKind.Error);
            MessageBox.Show(ex.Exception.Message, "MetroKafe Mklink", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppState.Load();
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
