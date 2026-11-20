using System.Text;
using System.Text.RegularExpressions;
using MetroKafeMklink.Models;

namespace MetroKafeMklink.Services;

/// <summary>
/// Secilen baglantilari tek bir .bat dosyasina yazar. Her kayit icin:
///   1. Hedef yoksa kaydi atlar (hata olarak loglar)
///   2. Baglanti yerinde junction varsa rmdir ile kaldirir
///   3. Gercek klasor varsa adina .eski ekler (varsa .eski_tarih) ve loglar
///   4. mklink /J ile baglar
/// </summary>
public static class BatExporter
{
    public static void Export(IReadOnlyList<LinkEntry> entries, string file)
    {
        var sb = new StringBuilder();
        void L(string line = "") => sb.Append(line).Append("\r\n");

        // Ilk satirlar ASCII kalmali; chcp sonrasindaki satirlar UTF-8 okunur
        L("@echo off");
        L("chcp 65001 >nul");
        L("setlocal");
        L("title MetroKafe Mklink");
        L($"REM MetroKafe Mklink {AppState.VersionText} ile olusturuldu: {DateTime.Now:yyyy-MM-dd HH:mm}");
        L("REM Her kayit icin: varsa junction kaldirilir, gercek klasorse adina .eski eklenir, sonra mklink /J yapilir.");
        L();
        L("fsutil dirty query %SystemDrive% >nul 2>&1 || (echo Bu dosya YONETICI olarak calistirilmalidir. & pause & exit /b 1)");
        L();
        L("REM Merkez klasor: gerekirse bu satiri degistirin");
        L($"set \"MERKEZ={AppState.CenterPath}\"");
        L("set \"LOG=%~dp0mklink_log.txt\"");
        L("set HATA=0");
        L("for /f %%i in ('powershell -NoProfile -Command \"Get-Date -Format yyyyMMdd_HHmmss\"') do set \"ZAMAN=%%i\"");
        L();
        L("REM Baglantilari kullanan programlari kapatmak icin asagidaki satirlarin basindaki REM'i silin");
        foreach (var name in AppState.Config.Settings.CloseProcesses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            L($"REM taskkill /f /im \"{(name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe")}\" >nul 2>&1");
        L();
        L($"call :log \"===== Basladi ({entries.Count} kayit) =====\"");
        foreach (var e in entries)
            L($"call :baglan \"{Clean(e.Name)}\" \"{ToBatPath(e.LinkPath)}\" \"{ToBatPath(e.TargetPath)}\"");
        L();
        L("if %HATA%==0 (call :log \"===== Tamamlandi =====\") else (call :log \"===== Bitti: %HATA% hata =====\")");
        L("echo.");
        L("echo Kayit dosyasi: %LOG%");
        L("pause");
        L("exit /b %HATA%");
        L();
        // Degiskenler parantezli bloklarin icinde genisletilirse ad/yoldaki ")" blogu bozar; bu yuzden goto ile akis
        L(":baglan");
        L("set \"AD=%~1\"");
        L("set \"LINK=%~2\"");
        L("set \"HEDEF=%~3\"");
        L("if exist \"%HEDEF%\\\" goto :b_hedef_var");
        L("call :log \"HATA   [%AD%] hedef klasor yok, atlandi: %HEDEF%\"");
        L("set /a HATA+=1");
        L("exit /b");
        L(":b_hedef_var");
        L("fsutil reparsepoint query \"%LINK%\" >nul 2>&1");
        L("if errorlevel 1 goto :b_junction_yok");
        L("rmdir \"%LINK%\"");
        L("fsutil reparsepoint query \"%LINK%\" >nul 2>&1");
        L("if not errorlevel 1 goto :b_rmdir_hata");
        L("call :log \"rmdir  [%AD%] mevcut mklink kaldirildi: %LINK%\"");
        L("goto :b_bagla");
        L(":b_junction_yok");
        L("if not exist \"%LINK%\\\" goto :b_bagla");
        L("for %%I in (\"%LINK%\") do set \"ESKI=%%~nxI.eski\"");
        L("if exist \"%LINK%.eski\" for %%I in (\"%LINK%\") do set \"ESKI=%%~nxI.eski_%ZAMAN%\"");
        L("ren \"%LINK%\" \"%ESKI%\"");
        L("if exist \"%LINK%\\\" goto :b_ren_hata");
        // :log icinde echo tirnaksiz calisir; ">" yonlendirme sayilacagi icin ok isareti olarak "→" kullanilir
        L("call :log \"ESKI   [%AD%] gercek klasor yeniden adlandirildi: %LINK% → %ESKI%\"");
        L(":b_bagla");
        L("for %%I in (\"%LINK%\") do if not exist \"%%~dpI\" mkdir \"%%~dpI\"");
        L("mklink /J \"%LINK%\" \"%HEDEF%\" >nul");
        L("if errorlevel 1 goto :b_mklink_hata");
        L("call :log \"MKLINK [%AD%] %LINK% → %HEDEF%\"");
        L("exit /b");
        L(":b_rmdir_hata");
        L("call :log \"HATA   [%AD%] mklink kaldirilamadi: %LINK%\"");
        L("set /a HATA+=1");
        L("exit /b");
        L(":b_ren_hata");
        L("call :log \"HATA   [%AD%] klasor yeniden adlandirilamadi (kullanimda olabilir): %LINK%\"");
        L("set /a HATA+=1");
        L("exit /b");
        L(":b_mklink_hata");
        L("call :log \"HATA   [%AD%] mklink olusturulamadi: %LINK%\"");
        L("set /a HATA+=1");
        L("exit /b");
        L();
        L(":log");
        L("echo %~1");
        L(">>\"%LOG%\" echo [%date% %time:~0,8%] %~1");
        L("exit /b");

        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>{MERKEZ} -> %MERKEZ%; %APPDATA% gibi degiskenler bat icinde oldugu gibi calisir.</summary>
    private static string ToBatPath(string path)
    {
        var p = Regex.Replace(path.Trim(), @"\{MERKEZ\}", "%MERKEZ%", RegexOptions.IgnoreCase);
        return Clean(p.TrimEnd('\\'));
    }

    /// <summary>Bat satirini bozabilecek karakterleri temizler.</summary>
    private static string Clean(string s) => Regex.Replace(s, "[\"&|<>^]", "");
}
