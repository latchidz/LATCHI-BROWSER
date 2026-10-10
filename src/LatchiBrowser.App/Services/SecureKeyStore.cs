using System.Text;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.App.Services;

/// <summary>
/// Gemini API key storage (§56/§57): Windows DPAPI (CurrentUser scope) — the key is
/// encrypted per-user/per-machine and NEVER stored in plain text, in settings.json,
/// in code, or in logs. Entered by the user in Settings, nothing else.
/// </summary>
public static class SecureKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("LATCHI-BROWSER-GEMINI-KEY-V1");

    private static string KeyPath => Path.Combine(AppPaths.DataDir, "gemini.key");

    public static bool HasKey() => File.Exists(KeyPath);

    public static void SaveKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("empty key");
        var dir = Path.GetDirectoryName(KeyPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var blob = System.Security.Cryptography.ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey.Trim()), Entropy,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        File.WriteAllBytes(KeyPath, blob);
        Logger.Info("Gemini API key stored (DPAPI, CurrentUser)"); // never the key itself
    }

    public static string? LoadKey()
    {
        try
        {
            if (!File.Exists(KeyPath)) return null;
            var blob = File.ReadAllBytes(KeyPath);
            var plain = System.Security.Cryptography.ProtectedData.Unprotect(blob, Entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            var key = Encoding.UTF8.GetString(plain).Trim();
            return key.Length > 0 ? key : null;
        }
        catch
        {
            return null; // corrupted or copied from another machine → treat as absent
        }
    }

    public static void DeleteKey()
    {
        try { if (File.Exists(KeyPath)) File.Delete(KeyPath); }
        catch { /* best effort */ }
    }
}
