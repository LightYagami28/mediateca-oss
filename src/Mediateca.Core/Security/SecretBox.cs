using System.Security.Cryptography;
using System.Text;

namespace Mediateca.Core.Security;

/// <summary>
/// Cifratura autenticata dei valori sensibili con AES-256-GCM.
/// </summary>
/// <remarks>
/// Sostituisce la combinazione Fernet+PBKDF2 dell'originale con una primitiva unica e standard:
/// la chiave è derivata dalla password dell'utente con PBKDF2-HMAC-SHA256 a 240k iterazioni e il
/// testo cifrato è <c>v1$salt$nonce$tag$payload</c> in base64, autodescrittivo e verificabile.
/// </remarks>
public sealed class SecretBox
{
    private const string Prefix = "v1";
    private const int SaltBytes = 16;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int KeyBytes = 32;
    private const int Iterations = 240_000;

    private readonly byte[] _key;

    private SecretBox(byte[] key) => _key = key;

    /// <summary>Deriva una cassaforte da una password e da un salt memorizzato.</summary>
    public static SecretBox FromPassword(string password, byte[] salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentNullException.ThrowIfNull(salt);
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return new SecretBox(key);
    }

    /// <summary>Crea un salt nuovo.</summary>
    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltBytes);

    /// <summary>Cifra un testo e restituisce una stringa autodescrittiva.</summary>
    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagBytes];

        using var aes = new AesGcm(_key, TagBytes);
        aes.Encrypt(nonce, plain, cipher, tag);

        return string.Join('$', Prefix, Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher));
    }

    /// <summary>Decifra una stringa prodotta da <see cref="Encrypt"/>.</summary>
    /// <exception cref="CryptographicException">Se il testo è stato manomesso o la chiave è sbagliata.</exception>
    public string Decrypt(string payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);
        var parts = payload.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
        {
            throw new CryptographicException("Formato del testo cifrato non riconosciuto.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var cipher = Convert.FromBase64String(parts[3]);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagBytes);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>Prova a decifrare, restituendo <paramref name="fallback"/> se il testo non è valido.</summary>
    public string TryDecrypt(string payload, string fallback = "") 
    {
        if (string.IsNullOrEmpty(payload))
        {
            return fallback;
        }

        try
        {
            return Decrypt(payload);
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
