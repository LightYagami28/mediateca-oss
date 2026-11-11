using System.Security.Cryptography;

namespace Teledisk.Licensing;

/// <summary>
/// Issues licenses by signing canonical payloads with an Ed25519 private key.
/// </summary>
/// <remarks>
/// The private key never lives in the repository: it is generated on the operator machine
/// and stored outside version control. Only the public key is distributed, and it is the
/// only thing a client needs to verify a license offline.
/// </remarks>
public static class LicenseSigner
{
    /// <summary>Algorithm identifier written into every envelope.</summary>
    public const string AlgorithmName = "RSA-PSS-SHA256";

    /// <summary>Extension used by license files.</summary>
    public const string LicenseFileExtension = ".tlicense";

    /// <summary>Creates a new RSA-2048 key pair.</summary>
    /// <param name="privateKeyPem">PKCS#8 PEM private key, to be stored outside the repository.</param>
    /// <param name="publicKeyPem">SPKI PEM public key, to be shipped with the application.</param>
    /// <param name="keyId">Stable identifier of the key, derived from the public key.</param>
    public static (string PrivateKeyPem, string PublicKeyPem, string KeyId) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        var privatePem = rsa.ExportPkcs8PrivateKeyPem();
        var publicPem = rsa.ExportSubjectPublicKeyInfoPem();
        return (privatePem, publicPem, ComputeKeyId(publicPem));
    }

    /// <summary>Derives the stable identifier of a public key.</summary>
    public static string ComputeKeyId(string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var raw = rsa.ExportSubjectPublicKeyInfo();
        return Convert.ToHexString(SHA256.HashData(raw))[..16].ToLowerInvariant();
    }

    /// <summary>Signs a document and returns the envelope to write on disk.</summary>
    public static LicenseEnvelope Issue(LicenseDocument document, string privateKeyPem, string keyId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        var payload = LicenseEnvelopeCodec.EncodePayload(document);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

        return new LicenseEnvelope
        {
            KeyId = keyId,
            Algorithm = AlgorithmName,
            Payload = LicenseEnvelopeCodec.ToBase64Url(payload),
            Signature = LicenseEnvelopeCodec.ToBase64Url(signature),
        };
    }

    /// <summary>Issues a document and returns the file content, ready to be saved as a license file.</summary>
    public static string IssueToFile(LicenseDocument document, string privateKeyPem, string keyId) =>
        LicenseEnvelopeCodec.EncodeEnvelope(Issue(document, privateKeyPem, keyId));
}
