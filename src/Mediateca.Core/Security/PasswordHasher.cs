using System.Security.Cryptography;
using System.Text;

namespace Mediateca.Core.Security;

/// <summary>
/// Hash delle password con PBKDF2-HMAC-SHA256 e formato versionato.
/// </summary>
/// <remarks>
/// Formato memorizzato: <c>pbkdf2-sha256$iterazioni$salt-base64$hash-base64</c>. Il prefisso
/// permette di cambiare algoritmo in futuro senza rompere le installazioni esistenti.
/// </remarks>
public static class PasswordHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int DefaultIterations = 240_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    /// <summary>Calcola l'hash di una password con un salt nuovo.</summary>
    public static string Hash(string password, int iterations = DefaultIterations)
    {
        if (password != null)
        {
            ArgumentException.ThrowIfNullOrEmpty(password);
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
            return $"{Prefix}${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    /// <summary>Verifica una password contro un hash memorizzato.</summary>
    public static bool Verify(string password, string stored)
    {
        if (stored != null)
        {
            if (password != null)
            {
                if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
                {
                    return false;
                }

                var parts = stored.Split('$');
                if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations))
                {
                    return false;
                }

                byte[] salt;
                byte[] expected;
                try
                {
                    salt = Convert.FromBase64String(parts[2]);
                    expected = Convert.FromBase64String(parts[3]);
                }
                catch (FormatException)
                {
                    return false;
                }

                var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    /// <summary>True quando la stringa è nel formato prodotto da <see cref="Hash"/>.</summary>
    public static bool IsHash(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix + "$", StringComparison.Ordinal);
}
