namespace MetroKafeMklink.Services;

/// <summary>
/// Pencere acmadan calisma: MetroKafeMklink.exe /bagla [/tasi] [/kayitsiz]
///   /bagla     etkin tum junction'lari kurar ve .reg dosyalarini ice aktarir
///   /tasi      gercek klasor varsa ve merkezde yoksa merkeze tasimaya da izin verir
///   /kayitsiz  registry ice aktarmayi atlar
/// Sonuc MetroKafeMklink.log dosyasina yazilir. Cikis kodu: 0 = sorunsuz, 1 = hata var.
/// </summary>
public static class CliRunner
{
    public static int Run(string[] args)
    {
        bool Has(string flag) => args.Any(a => a.TrimStart('/', '-').Equals(flag, StringComparison.OrdinalIgnoreCase));
        var allowMove = Has("tasi");
        var skipRegistry = Has("kayitsiz");
        var errors = 0;

        AppState.Load();
        AppState.AddLog("Komut satırı: bağlama başladı");
        ProcessRunner.KillProcesses(AppState.Config.Settings.CloseProcesses);

        foreach (var e in AppState.Config.Links.Where(l => l.Enabled))
        {
            try
            {
                JunctionService.Refresh(e);
                var action = JunctionService.PlanAction(e);
                if (action == LinkAction.None) continue;
                if (action == LinkAction.Error)
                {
                    errors++;
                    AppState.AddLog($"{e.Name}: {e.Detail}", LogKind.Error);
                    continue;
                }
                if (action == LinkAction.MoveAndLink && !allowMove)
                {
                    AppState.AddLog($"{e.Name}: merkeze taşıma gerekiyor, /tasi verilmediği için atlandı", LogKind.Warning);
                    continue;
                }
                JunctionService.Apply(e, action);
                AppState.AddLog($"{e.Name}: {JunctionService.DescribeDone(action)}", LogKind.Success);
            }
            catch (Exception ex)
            {
                errors++;
                AppState.AddLog($"{e.Name}: {ex.Message}", LogKind.Error);
            }
        }

        if (!skipRegistry)
        {
            foreach (var r in AppState.Config.Registry.Where(r => r.Enabled))
            {
                try
                {
                    RegistryService.Refresh(r);
                    if (!r.FileExists)
                    {
                        AppState.AddLog($"{r.Name}: .reg dosyası yok, atlandı", LogKind.Warning);
                        continue;
                    }
                    RegistryService.Import(r);
                    AppState.AddLog($"{r.Name}: içe aktarıldı", LogKind.Success);
                }
                catch (Exception ex)
                {
                    errors++;
                    AppState.AddLog($"{r.Name}: {ex.Message}", LogKind.Error);
                }
            }
        }

        AppState.AddLog(errors == 0 ? "Komut satırı: tamamlandı" : $"Komut satırı: {errors} hata ile bitti",
                        errors == 0 ? LogKind.Success : LogKind.Error);
        return errors == 0 ? 0 : 1;
    }
}
