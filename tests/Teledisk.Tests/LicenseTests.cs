using System.Security.Cryptography;
using Teledisk.Licensing;

namespace Teledisk.Tests;

/// <summary>
/// Copre emissione, verifica, binding macchina/prodotto, chiavi fidate e fallback del sistema di licenza.
/// </summary>
public sealed class LicenseTests : IDisposable
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef";

    private static readonly LicenseFeatures[] PaidFeatures =
    [
        LicenseFeatures.TelegramBot,
        LicenseFeatures.RemoteAccess,
        LicenseFeatures.CloudBackup,
        LicenseFeatures.MultiBot,
        LicenseFeatures.MetadataProviders,
        LicenseFeatures.ScheduledBackup,
    ];

    private static readonly LicenseFeatures[] AllFeatureFlags =
    [
        LicenseFeatures.CoreLibrary,
        .. PaidFeatures,
    ];

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "teledisk-license-tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>Prepara una cartella temporanea isolata per i file di prova.</summary>
    public LicenseTests() => Directory.CreateDirectory(_temporaryDirectory);

    /// <summary>Elimina la cartella temporanea, ignorando gli eventuali file ancora bloccati.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Emette un documento di prova e restituisce la busta firmata.</summary>
    private static LicenseEnvelope Issue(
        string privateKeyPem,
        string keyId,
        string machine = Fingerprint,
        string product = LicenseAuthority.ProductId,
        LicenseTier tier = LicenseTier.Pro,
        DateTimeOffset? issuedAt = null,
        DateTimeOffset? expiresAt = null)
    {
        var document = new LicenseDocument
        {
            Id = $"tl-test-{Guid.NewGuid().ToString("N")[..8]}",
            Product = product,
            Tier = tier,
            Subject = "test@example.com",
            IssuedAt = issuedAt ?? DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresAt = expiresAt,
            Machine = machine,
        };

        return LicenseSigner.Issue(document, privateKeyPem, keyId);
    }

    [Fact]
    public void IssueAndVerify_WithGeneratedKeyPair_RoundTripsAsValid()
    {
        var (privateKeyPem, publicKeyPem, keyId) = LicenseSigner.GenerateKeyPair();
        var verifier = new LicenseVerifier(keyId, publicKeyPem);
        var envelope = Issue(privateKeyPem, keyId);

        var direct = verifier.Verify(envelope, Fingerprint);

        Assert.Equal(LicenseStatusCode.Valid, direct.Code);
        Assert.True(direct.IsUsable);
        Assert.NotNull(direct.Document);
        Assert.Equal("test@example.com", direct.Document!.Subject);
        Assert.Equal(LicenseTier.Pro, direct.Document.Tier);
        Assert.Equal(keyId, direct.KeyId);
        Assert.False(string.IsNullOrWhiteSpace(direct.PayloadDigest));

        var path = Path.Combine(_temporaryDirectory, "roundtrip" + LicenseSigner.LicenseFileExtension);
        File.WriteAllText(path, LicenseEnvelopeCodec.EncodeEnvelope(envelope));

        var fromFile = verifier.VerifyFile(path, Fingerprint);

        Assert.Equal(LicenseStatusCode.Valid, fromFile.Code);
        Assert.Equal(direct.PayloadDigest, fromFile.PayloadDigest);
    }

    [Fact]
    public void TamperedPayload_IsRejectedAsSignatureInvalid()
    {
        var (privateKeyPem, publicKeyPem, keyId) = LicenseSigner.GenerateKeyPair();
        var envelope = Issue(privateKeyPem, keyId);

        var document = LicenseEnvelopeCodec.DecodePayload(LicenseEnvelopeCodec.FromBase64Url(envelope.Payload));
        var tampered = document with { Subject = "attacker@example.com" };
        var rewritten = envelope with
        {
            Payload = LicenseEnvelopeCodec.ToBase64Url(LicenseEnvelopeCodec.EncodePayload(tampered)),
        };

        var result = new LicenseVerifier(keyId, publicKeyPem).Verify(rewritten, Fingerprint);

        Assert.Equal(LicenseStatusCode.SignatureInvalid, result.Code);
        Assert.False(result.IsUsable);
        Assert.Null(result.Document);
    }

    [Fact]
    public void ExpiredLicense_IsReportedAsExpired()
    {
        var (privateKeyPem, publicKeyPem, keyId) = LicenseSigner.GenerateKeyPair();
        var expiry = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var verifier = new LicenseVerifier(keyId, publicKeyPem);
        var envelope = Issue(privateKeyPem, keyId, issuedAt: expiry.AddDays(-30), expiresAt: expiry);

        Assert.Equal(LicenseStatusCode.Valid, verifier.Verify(envelope, Fingerprint, expiry.AddHours(-1)).Code);

        var result = verifier.Verify(envelope, Fingerprint, expiry.AddSeconds(1));

        Assert.Equal(LicenseStatusCode.Expired, result.Code);
        Assert.False(result.IsUsable);
        Assert.NotNull(result.Document);
        Assert.Equal(expiry, result.Document!.ExpiresAt);
    }

    [Fact]
    public void DifferentMachine_IsRejectedAsMachineMismatch()
    {
        var (privateKeyPem, publicKeyPem, keyId) = LicenseSigner.GenerateKeyPair();
        var verifier = new LicenseVerifier(keyId, publicKeyPem);
        var envelope = Issue(privateKeyPem, keyId, machine: "machine-a");

        Assert.Equal(LicenseStatusCode.Valid, verifier.Verify(envelope, "machine-a").Code);

        var result = verifier.Verify(envelope, "machine-b");

        Assert.Equal(LicenseStatusCode.MachineMismatch, result.Code);
        Assert.False(result.IsUsable);
    }

    [Fact]
    public void DifferentProduct_IsRejectedAsProductMismatch()
    {
        var (privateKeyPem, publicKeyPem, keyId) = LicenseSigner.GenerateKeyPair();
        var envelope = Issue(privateKeyPem, keyId, product: "another-product");

        var result = new LicenseVerifier(keyId, publicKeyPem).Verify(envelope, Fingerprint);

        Assert.Equal(LicenseStatusCode.ProductMismatch, result.Code);
        Assert.False(result.IsUsable);
    }

    [Fact]
    public void UntrustedKeyId_IsRejectedAsSignatureInvalid()
    {
        var (privateKeyPem, _, keyId) = LicenseSigner.GenerateKeyPair();
        var (_, otherPublicKeyPem, otherKeyId) = LicenseSigner.GenerateKeyPair();
        var envelope = Issue(privateKeyPem, keyId);

        var result = new LicenseVerifier(otherKeyId, otherPublicKeyPem).Verify(envelope, Fingerprint);

        Assert.Equal(LicenseStatusCode.SignatureInvalid, result.Code);
        Assert.Contains(result.Reasons, reason => reason.Contains("not trusted", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultFeatures_ForEveryTier_IncludeEveryFeature()
    {
        // Il progetto è pubblico al 100%: nessun tier lascia indietro una funzionalità.
        foreach (var tier in new[] { LicenseTier.Community, LicenseTier.Pro, LicenseTier.Patron })
        {
            var features = LicenseAuthority.DefaultFeatures(tier);
            Assert.Equal(LicenseFeatures.All, features);
            Assert.All(AllFeatureFlags, feature => Assert.True(features.HasFlag(feature)));
        }
    }

    [Fact]
    public void ValidProLicense_KeepsItsTierLabelAndStillGrantsEveryFeature()
    {
        // La licenza non sblocca nulla: aggiunge solo l'etichetta del tier e l'identità.
        foreach (var tier in new[] { LicenseTier.Pro, LicenseTier.Patron })
        {
            var (privateKeyPem, publicKeyPem, keyId) = LicenseSigner.GenerateKeyPair();
            var path = Path.Combine(_temporaryDirectory, $"valid-{tier.ToString().ToLowerInvariant()}{LicenseSigner.LicenseFileExtension}");
            var document = new LicenseDocument
            {
                Id = $"tl-test-{tier.ToString().ToLowerInvariant()}",
                Tier = tier,
                Subject = "supporter@example.com",
                IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                Machine = Fingerprint,
            };
            File.WriteAllText(path, LicenseSigner.IssueToFile(document, privateKeyPem, keyId));

            WithEnvironment(path, () =>
            {
                var authority = new LicenseAuthority(
                    new LicenseVerifier(keyId, publicKeyPem),
                    machineFingerprint: () => Fingerprint);

                var status = authority.Resolve();

                Assert.Equal(tier, status.Tier);
                Assert.True(status.HasLicense);
                Assert.False(status.IsCommunity);
                Assert.Equal(LicenseFeatures.All, status.Features);
                Assert.All(AllFeatureFlags, feature => Assert.True(authority.Allows(feature)));
            });
        }
    }

    [Fact]
    public void AuthorityWithoutLicenseFile_DoesNotThrowAndGrantsEveryFeature()
    {
        var home = Path.Combine(_temporaryDirectory, "home-without-license");
        Directory.CreateDirectory(home);

        var previousHome = SwapEnvironment(LicenseAuthority.HomeVariable, home);
        var previousLicensePath = SwapEnvironment(LicenseAuthority.LicensePathVariable, null);
        var previousTrusted = SwapEnvironment(TrustedKeys.TrustedFolderVariable, null);
        try
        {
            var authority = LicenseAuthority.CreateDefault();
            var status = authority.Resolve();

            Assert.Equal(LicenseTier.Community, status.Tier);
            Assert.False(status.HasLicense);
            Assert.True(status.IsCommunity);
            Assert.Equal(LicenseStatusCode.NotFound, status.Code);
            Assert.Equal(LicenseFeatures.All, status.Features);
            Assert.All(AllFeatureFlags, feature => Assert.True(status.Features.HasFlag(feature)));
            Assert.All(AllFeatureFlags, feature => Assert.True(authority.Allows(feature)));
            Assert.NotEmpty(status.Reasons);
        }
        finally
        {
            RestoreEnvironment(LicenseAuthority.HomeVariable, previousHome);
            RestoreEnvironment(LicenseAuthority.LicensePathVariable, previousLicensePath);
            RestoreEnvironment(TrustedKeys.TrustedFolderVariable, previousTrusted);
        }
    }

    [Fact]
    public void CommunityIssuer_IsAValidRsaPublicKey()
    {
        Assert.Single(ProjectKeys.All);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(ProjectKeys.CommunityIssuer);
        Assert.Equal(2048, rsa.KeySize);

        var keyId = TrustedKeys.ComputeKeyId(ProjectKeys.CommunityIssuer);
        Assert.Matches("^[0-9a-f]{16}$", keyId);
        Assert.Equal(LicenseSigner.ComputeKeyId(ProjectKeys.CommunityIssuer), keyId);

        var embedded = TrustedKeys.EmbeddedProjectKeys().ToList();
        Assert.Single(embedded);
        Assert.Equal(keyId, embedded[0].KeyId);
    }

    [Fact]
    public void TrustedKeysLoad_ToleratesBrokenExtraKeysAndKeepsTheOthers()
    {
        var folder = Path.Combine(_temporaryDirectory, "trusted-with-broken-keys");
        Directory.CreateDirectory(folder);
        const string garbage = "this is not a pem";
        const string truncated = "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----";
        File.WriteAllText(Path.Combine(folder, "garbage.pem"), garbage);
        File.WriteAllText(Path.Combine(folder, "truncated.pem"), truncated);

        // Un PEM rotto non lancia e riceve comunque un identificatore stabile e distinto.
        var garbageId = TrustedKeys.ComputeKeyId(garbage);
        var truncatedId = TrustedKeys.ComputeKeyId(truncated);
        Assert.Matches("^[0-9a-f]{16}$", garbageId);
        Assert.NotEqual(garbageId, truncatedId);

        var previousTrusted = SwapEnvironment(TrustedKeys.TrustedFolderVariable, folder);
        try
        {
            var keys = TrustedKeys.Load();

            Assert.True(keys.ContainsKey(TrustedKeys.ComputeKeyId(ProjectKeys.CommunityIssuer)));
            Assert.True(keys.ContainsKey(garbageId));
            Assert.True(keys.ContainsKey(truncatedId));
        }
        finally
        {
            RestoreEnvironment(TrustedKeys.TrustedFolderVariable, previousTrusted);
        }
    }

    /// <summary>Esegue <paramref name="body"/> con la licenza indicata come unico file visibile.</summary>
    private void WithEnvironment(string licensePath, Action body)
    {
        var previousHome = SwapEnvironment(LicenseAuthority.HomeVariable, _temporaryDirectory);
        var previousLicensePath = SwapEnvironment(LicenseAuthority.LicensePathVariable, licensePath);
        var previousTrusted = SwapEnvironment(TrustedKeys.TrustedFolderVariable, null);
        try
        {
            body();
        }
        finally
        {
            RestoreEnvironment(LicenseAuthority.HomeVariable, previousHome);
            RestoreEnvironment(LicenseAuthority.LicensePathVariable, previousLicensePath);
            RestoreEnvironment(TrustedKeys.TrustedFolderVariable, previousTrusted);
        }
    }

    /// <summary>Imposta una variabile d'ambiente e restituisce il valore precedente.</summary>
    private static string? SwapEnvironment(string name, string? value)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        return previous;
    }

    /// <summary>Ripristina una variabile d'ambiente, rimuovendola quando prima non esisteva.</summary>
    private static void RestoreEnvironment(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value);
}
