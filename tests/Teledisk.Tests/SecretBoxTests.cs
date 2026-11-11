using System.Security.Cryptography;
using System.Text;
using Teledisk.Core.Security;

namespace Teledisk.Tests;

/// <summary>Test di <see cref="SecretBox"/>: round-trip, isolamento del salt e manomissioni.</summary>
public sealed class SecretBoxTests
{
    private static SecretBox NewBox(string password = "password-di-prova") =>
        SecretBox.FromPassword(password, SecretBox.NewSalt());

    /// <summary>Il testo cifrato e decifrato torna identico, anche con unicode e payload lunghi.</summary>
    [Fact]
    public void RoundTrip_PreservaIlTesto()
    {
        var box = NewBox();

        foreach (var text in new[] { "semplice", "", "Città è — unicode 🎬", new string('a', 4096) })
        {
            Assert.Equal(text, box.Decrypt(box.Encrypt(text)));
        }
    }

    /// <summary>Il payload è autodescrittivo: prefisso, nonce, tag e testo cifrato in base64.</summary>
    [Fact]
    public void Encrypt_ProducePayloadVersionato()
    {
        var payload = NewBox().Encrypt("testo");

        var parts = payload.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.Equal(12, Convert.FromBase64String(parts[1]).Length);
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(Encoding.UTF8.GetByteCount("testo"), Convert.FromBase64String(parts[3]).Length);
    }

    /// <summary>Due salt diversi producono chiavi diverse: il payload non è decifrabile.</summary>
    [Fact]
    public void SaltDiverso_NonDecifra()
    {
        var first = NewBox();
        var second = NewBox();
        var payload = first.Encrypt("segreto");

        Assert.ThrowsAny<CryptographicException>(() => second.Decrypt(payload));
        Assert.Equal("fallback", second.TryDecrypt(payload, "fallback"));
    }

    /// <summary>Un payload manomesso in qualunque sua parte non supera il controllo di autenticazione.</summary>
    [Fact]
    public void PayloadManomesso_ProduceEccezione()
    {
        var box = NewBox();
        var payload = box.Encrypt("segreto");
        var parts = payload.Split('$');

        var prefissoAlterato = $"v2${parts[1]}${parts[2]}${parts[3]}";
        var nonceAlterato = $"v1${Flip(parts[1])}${parts[2]}${parts[3]}";
        var tagAlterato = $"v1${parts[1]}${Flip(parts[2])}${parts[3]}";
        var cifratoAlterato = $"v1${parts[1]}${parts[2]}${Flip(parts[3])}";
        const string componentiMancanti = "v1$solo-uno";

        Assert.Throws<CryptographicException>(() => box.Decrypt(prefissoAlterato));
        Assert.ThrowsAny<CryptographicException>(() => box.Decrypt(nonceAlterato));
        Assert.ThrowsAny<CryptographicException>(() => box.Decrypt(tagAlterato));
        Assert.ThrowsAny<CryptographicException>(() => box.Decrypt(cifratoAlterato));
        Assert.Throws<CryptographicException>(() => box.Decrypt(componentiMancanti));
    }

    /// <summary>Base64 non valido produce comunque un'eccezione.</summary>
    [Fact]
    public void PayloadConBase64NonValido_ProduceEccezione()
    {
        Assert.ThrowsAny<Exception>(() => NewBox().Decrypt("v1$***$***$***"));
    }

    /// <summary><see cref="SecretBox.TryDecrypt"/> non lancia mai e usa il fallback.</summary>
    [Fact]
    public void TryDecrypt_NonLanciaMai()
    {
        var box = NewBox();

        Assert.Equal("", box.TryDecrypt(""));
        Assert.Equal("casa", box.TryDecrypt(null!, "casa"));
        Assert.Equal("valore", box.TryDecrypt("spazzatura", "valore"));
        Assert.Equal("valore", box.TryDecrypt("v1$***$***$***", "valore"));
        Assert.Equal("ok", box.TryDecrypt(box.Encrypt("ok"), "valore"));
    }

    /// <summary>Ogni chiamata a <see cref="SecretBox.NewSalt"/> produce 16 byte casuali.</summary>
    [Fact]
    public void NewSalt_ProduceSaltCasuale()
    {
        var first = SecretBox.NewSalt();
        var second = SecretBox.NewSalt();

        Assert.Equal(16, first.Length);
        Assert.NotEqual(Convert.ToBase64String(first), Convert.ToBase64String(second));
    }

    /// <summary>Password e salt nulli o vuoti sono rifiutati con l'eccezione giusta.</summary>
    [Fact]
    public void FromPassword_ValidaGliArgomenti()
    {
        Assert.Throws<ArgumentException>(() => SecretBox.FromPassword("", SecretBox.NewSalt()));
        Assert.Throws<ArgumentNullException>(() => SecretBox.FromPassword(null!, SecretBox.NewSalt()));
        Assert.Throws<ArgumentNullException>(() => SecretBox.FromPassword("x", null!));
    }

    /// <summary>Cifratura e decifratura validano l'input.</summary>
    [Fact]
    public void EncryptEDecrypt_ValidanoLInput()
    {
        var box = NewBox();

        Assert.Throws<ArgumentNullException>(() => box.Encrypt(null!));
        Assert.Throws<ArgumentException>(() => box.Decrypt(""));
    }

    private static string Flip(string text) => (text[0] == 'A' ? 'B' : 'A') + text[1..];
}
