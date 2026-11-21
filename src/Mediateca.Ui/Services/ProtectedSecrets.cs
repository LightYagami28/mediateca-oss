using System.Security.Cryptography;
using System.Text;

namespace Mediateca.Ui.Services;

public static class ProtectedSecrets
{
    public static string Encrypt(string plaintext)
    {
        if (plaintext != null)
        {
            if (string.IsNullOrEmpty(plaintext))
            {
                return string.Empty;
            }

            var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    public static string Decrypt(string payload)
    {
        if (payload != null)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return string.Empty;
            }

            try
            {
                var plain = ProtectedData.Unprotect(Convert.FromBase64String(payload), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }
}
