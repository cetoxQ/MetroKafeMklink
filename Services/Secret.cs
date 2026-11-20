using System.Security.Cryptography;
using System.Text;

namespace MetroKafeMklink.Services;

/// <summary>Token gibi gizli degerleri bu bilgisayara ozel (DPAPI) sifreler.</summary>
public static class Secret
{
    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.LocalMachine);
        return Convert.ToBase64String(data);
    }

    public static string Unprotect(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        try
        {
            var data = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            // Baska bilgisayarda sifrelenmis olabilir
            return "";
        }
    }
}
