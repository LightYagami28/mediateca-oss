using Mediateca.Core.Security;

namespace Mediateca.Tests;

/// <summary>Test di <see cref="PasswordHasher"/>: formato versionato, verifica e robustezza.</summary>
public sealed class PasswordHasherTests
{
    /// <summary>Un hash appena calcolato ha il formato versionato atteso.</summary>
    [Fact]
    public void Hash_ProduceFormatoVersionato()
    {
        var hash = PasswordHasher.Hash("password-di-prova", iterations: 1_000);

        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("pbkdf2-sha256", parts[0]);
        Assert.Equal(1_000, int.Parse(parts[1]));
        Assert.Equal(16, Convert.FromBase64String(parts[2]).Length);
        Assert.Equal(32, Convert.FromBase64String(parts[3]).Length);
        Assert.True(PasswordHasher.IsHash(hash));
    }

    /// <summary>Le iterazioni predefinite sono quelle dichiarate dal codice.</summary>
    [Fact]
    public void Hash_UsaLeIterazioniPredefinite()
    {
        var hash = PasswordHasher.Hash("password");
        Assert.Equal(240_000, int.Parse(hash.Split('$')[1]));
    }

    /// <summary>Ogni hash usa un salt nuovo: due hash della stessa password differiscono.</summary>
    [Fact]
    public void Hash_UsaSaltCasuale()
    {
        var first = PasswordHasher.Hash("stessa-password", iterations: 1_000);
        var second = PasswordHasher.Hash("stessa-password", iterations: 1_000);

        Assert.NotEqual(first, second);
        Assert.True(PasswordHasher.Verify("stessa-password", first));
        Assert.True(PasswordHasher.Verify("stessa-password", second));
    }

    /// <summary>La verifica accetta la password giusta e rifiuta quella sbagliata.</summary>
    [Fact]
    public void Verify_DistinguePasswordGiustaESbagliata()
    {
        var hash = PasswordHasher.Hash("giusta", iterations: 1_000);

        Assert.True(PasswordHasher.Verify("giusta", hash));
        Assert.False(PasswordHasher.Verify("sbagliata", hash));
        Assert.False(PasswordHasher.Verify("GIUSTA", hash));
        Assert.False(PasswordHasher.Verify("", hash));
        Assert.False(PasswordHasher.Verify("giusta", ""));
    }

    /// <summary>Le iterazioni memorizzate sono riusate in verifica.</summary>
    [Fact]
    public void Verify_RiusaLeIterazioniMemorizzate()
    {
        var hash = PasswordHasher.Hash("password", iterations: 500);

        Assert.Equal(500, int.Parse(hash.Split('$')[1]));
        Assert.True(PasswordHasher.Verify("password", hash));
    }

    /// <summary>Valori non nel formato atteso vengono rifiutati senza eccezioni.</summary>
    [Theory]
    [InlineData("testo-in-chiaro")]
    [InlineData("pbkdf2-sha256$non-un-numero$AAAA$BBBB")]
    [InlineData("pbkdf2-sha256$1000$non-base64!$BBBB")]
    [InlineData("pbkdf2-sha256$1000$AAAA")]
    [InlineData("altro-prefisso$1000$AAAA$BBBB")]
    [InlineData("")]
    public void Verify_RifiutaHashMalformati(string stored)
    {
        Assert.False(PasswordHasher.Verify("password", stored));
    }

    /// <summary>Una password vuota o nulla non è hashabile.</summary>
    [Fact]
    public void Hash_RifiutaPasswordVuota()
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.Hash(""));
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Hash(null!));
    }

    /// <summary><see cref="PasswordHasher.IsHash"/> riconosce solo il prefisso versionato.</summary>
    [Fact]
    public void IsHash_RiconosceSoloIlPrefisso()
    {
        Assert.True(PasswordHasher.IsHash(PasswordHasher.Hash("x", iterations: 500)));
        Assert.False(PasswordHasher.IsHash("testo-in-chiaro"));
        Assert.False(PasswordHasher.IsHash("pbkdf2-sha512$1000$AAAA$BBBB"));
        Assert.False(PasswordHasher.IsHash(""));
        Assert.False(PasswordHasher.IsHash(null));
    }
}
