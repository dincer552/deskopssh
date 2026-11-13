using System.Security.Cryptography;
using System.Text;

namespace HattrickSSH.Services;

public sealed class SecureKeyStore(LocalStore store)
{
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes("HattrickSSH.PrivateKey.v1"));
    public string Import(string sourcePath)
    {
        var raw = File.ReadAllBytes(sourcePath);
        var text = Encoding.UTF8.GetString(raw);
        if (!text.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("PuTTY-User-Key-File", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Dosya tanınan bir SSH private key biçiminde değil.");
        var id = Guid.NewGuid().ToString("N");
        var protectedBytes = ProtectedData.Protect(raw, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(Path.Combine(store.KeyDirectory, id + ".dpapi"), protectedBytes);
        CryptographicOperations.ZeroMemory(raw);
        return id;
    }

    public string CreateTemporaryKey(string keyId)
    {
        var path = Path.Combine(store.KeyDirectory, keyId + ".dpapi");
        if (!File.Exists(path)) throw new FileNotFoundException("Kayıtlı SSH anahtarı bulunamadı.");
        var clear = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        var folder = Path.Combine(Path.GetTempPath(), "HattrickSSH", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var tempPath = Path.Combine(folder, "identity.key");
        try
        {
            File.WriteAllBytes(tempPath, clear);
            RestrictFilePermissions(tempPath);
            return tempPath;
        }
        catch
        {
            try { Directory.Delete(folder, true); } catch { }
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public void DeleteTemporaryKey(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var folder = Path.GetDirectoryName(path);
            if (File.Exists(path)) File.Delete(path);
            if (folder is not null && Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        catch { }
    }

    public void Delete(string keyId)
    {
        try { File.Delete(Path.Combine(store.KeyDirectory, keyId + ".dpapi")); } catch { }
    }

    private static void RestrictFilePermissions(string path)
    {
        // OpenSSH for Windows rejects private keys readable by other users.
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        var start = new System.Diagnostics.ProcessStartInfo("icacls.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("/inheritance:r");
        start.ArgumentList.Add("/grant:r");
        start.ArgumentList.Add(identity + ":(R)");
        start.ArgumentList.Add("SYSTEM:(F)");
        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("icacls başlatılamadı.");
        process.WaitForExit(5000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException("SSH anahtar dosyası Windows kullanıcı izinleriyle korunamadı.");
    }
}
