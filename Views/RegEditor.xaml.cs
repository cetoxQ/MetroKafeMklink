using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MetroKafeMklink.Models;
using MetroKafeMklink.Services;

namespace MetroKafeMklink.Views;

public partial class RegEditor : UserControl, IDialogBody
{
    private readonly RegEntry _entry;

    public RegEditor(RegEntry entry)
    {
        InitializeComponent();
        _entry = entry;
        DataContext = entry;
        UpdateKeyState();
    }

    private void Key_TextChanged(object sender, TextChangedEventArgs e) => UpdateKeyState();

    private void UpdateKeyState()
    {
        if (KeyState == null) return;
        var key = NormalizeKey(KeyBox.Text);
        if (key.Length == 0) { KeyState.Text = ""; return; }
        if (!RegistryService.TryParse(key, out _, out _, out _)) { KeyState.Text = "Kök tanınmadı (HKLM, HKCU, HKCR, HKU olmalı)"; return; }
        KeyState.Text = RegistryService.KeyExists(key) ? "✓ Bu bilgisayarda var" : "Bu bilgisayarda yok";
    }

    /// <summary>Regedit adres cubugundan gelen "Bilgisayar\" / "Computer\" onekini atar.</summary>
    private static string NormalizeKey(string key)
    {
        var k = key.Trim().Trim('\\');
        var i = k.IndexOf('\\');
        if (i > 0 && !k[..i].StartsWith("HK", StringComparison.OrdinalIgnoreCase)) k = k[(i + 1)..];
        return k;
    }

    public bool Validate()
    {
        _entry.Name = _entry.Name.Trim();
        _entry.Group = _entry.Group.Trim();
        _entry.Key = NormalizeKey(_entry.Key);
        _entry.FileName = _entry.FileName.Trim();

        if (_entry.Name.Length == 0) return ShowError("Ad boş olamaz.");
        if (!RegistryService.TryParse(_entry.Key, out _, out var sub, out _)) return ShowError("Anahtar HKLM, HKCU, HKCR ya da HKU ile başlamalı.");
        if (sub.Length == 0) return ShowError("Kök anahtarın tamamı aktarılamaz; bir alt anahtar yazın.");

        if (_entry.FileName.Length == 0)
            _entry.FileName = Regex.Replace(_entry.Name.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_') + ".reg";
        if (!_entry.FileName.EndsWith(".reg", StringComparison.OrdinalIgnoreCase)) _entry.FileName += ".reg";
        if (_entry.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return ShowError("Dosya adında geçersiz karakter var.");

        var clash = AppState.Config.Registry.Any(r => !ReferenceEquals(r, _entry)
            && string.Equals(r.FileName, _entry.FileName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(r.Key, _entry.Key, StringComparison.OrdinalIgnoreCase));
        if (clash) return ShowError("Bu dosya adı başka bir anahtar için kullanılıyor.");
        return true;
    }

    private bool ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        return false;
    }
}
