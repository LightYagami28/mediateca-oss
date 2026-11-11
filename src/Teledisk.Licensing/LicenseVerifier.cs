using System.Security.Cryptography;
using System.Text;

namespace Teledisk.Licensing;

/// <summary>
/// Outcome of a license verification.
/// </summary>
public enum LicenseStatusCode
{
    /// <summary>The license is valid and grants the requested capability.</summary>
    Valid = 0,

    /// <summary>No license file was found: the application runs in Community mode.</summary>
    NotFound = 1,

    /// <summary>The license file could not be parsed.</summary>
    Malformed = 2,

    /// <summary>The signature does not match the payload, or was produced by an unknown key.</summary>
    SignatureInvalid = 3,

    /// <summary>The license is valid but expired.</summary>
    Expired = 4,

    /// <summary>The license is bound to a different machine.</summary>
    MachineMismatch = 5,

    /// <summary>The license was issued for a different product.</summary>
    ProductMismatch = 6,

    /// <summary>The license uses an unsupported algorithm or format version.</summary>
    Unsupported = 7,
}

/// <summary>
/// Result of verifying a license envelope.
/// </summary>
/// <param name="Code">Outcome code.</param>
/// <param name="Reasons">Human readable explanations, suitable for logs and for the CLI.</param>
/// <param name="Document">The decoded document, when it could be read.</param>
/// <param name="KeyId">Identifier of the key that was expected to have signed the license.</param>
/// <param name="PayloadDigest">SHA-256 of the canonical payload, for audit trails.</param>
public sealed record LicenseVerification(
    LicenseStatusCode Code,
    IReadOnlyList<string> Reasons,
    LicenseDocument? Document,
    string? KeyId,
    string? PayloadDigest)
{
    /// <summary>True when the license was cryptographically valid and not expired.</summary>
    public bool IsUsable => Code is LicenseStatusCode.Valid;
}

/// <summary>
/// Verifies licenses offline against a set of trusted public keys.
/// </summary>
public sealed class LicenseVerifier
{
    private readonly IReadOnlyDictionary<string, string> _trustedKeys;

    /// <summary>Creates a verifier trusting the supplied key identifiers and public keys.</summary>
    public LicenseVerifier(IReadOnlyDictionary<string, string> trustedKeys)
    {
        ArgumentNullException.ThrowIfNull(trustedKeys);
        _trustedKeys = trustedKeys;
    }

    /// <summary>Creates a verifier trusting a single public key.</summary>
    public LicenseVerifier(string keyId, string publicKeyPem)
        : this(new Dictionary<string, string>(StringComparer.Ordinal) { [keyId] = publicKeyPem })
    {
    }

    /// <summary>Verifies an envelope.</summary>
    /// <param name="envelope">The envelope to verify.</param>
    /// <param name="machineFingerprint">Fingerprint of the current machine, used for device binding.</param>
    /// <param name="now">Current time, injectable for tests.</param>
    public LicenseVerification Verify(
        LicenseEnvelope envelope,
        string machineFingerprint,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var reasons = new List<string>();

        if (envelope.Version != 1)
        {
            reasons.Add($"Unsupported envelope version {envelope.Version}.");
            return new LicenseVerification(LicenseStatusCode.Unsupported, reasons, null, envelope.KeyId, null);
        }

        if (!string.Equals(envelope.Algorithm, LicenseSigner.AlgorithmName, StringComparison.Ordinal))
        {
            reasons.Add($"Unsupported algorithm '{envelope.Algorithm}'.");
            return new LicenseVerification(LicenseStatusCode.Unsupported, reasons, null, envelope.KeyId, null);
        }

        if (!_trustedKeys.TryGetValue(envelope.KeyId, out var publicKeyPem))
        {
            reasons.Add($"The signing key '{envelope.KeyId}' is not trusted.");
            return new LicenseVerification(LicenseStatusCode.SignatureInvalid, reasons, null, envelope.KeyId, null);
        }

        byte[] payload;
        byte[] signature;
        try
        {
            payload = LicenseEnvelopeCodec.FromBase64Url(envelope.Payload);
            signature = LicenseEnvelopeCodec.FromBase64Url(envelope.Signature);
        }
        catch (FormatException exc)
        {
            reasons.Add($"The license file is not readable: {exc.Message}");
            return new LicenseVerification(LicenseStatusCode.Malformed, reasons, null, envelope.KeyId, null);
        }

        var digest = LicenseEnvelopeCodec.PayloadDigest(payload);

        bool signatureValid;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            signatureValid = rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception exc)
        {
            reasons.Add($"The trusted public key could not be used: {exc.Message}");
            return new LicenseVerification(LicenseStatusCode.SignatureInvalid, reasons, null, envelope.KeyId, digest);
        }

        if (!signatureValid)
        {
            reasons.Add("The signature does not match the license content.");
            return new LicenseVerification(LicenseStatusCode.SignatureInvalid, reasons, null, envelope.KeyId, digest);
        }

        LicenseDocument document;
        try
        {
            document = LicenseEnvelopeCodec.DecodePayload(payload);
        }
        catch (Exception exc)
        {
            reasons.Add($"The license payload could not be decoded: {exc.Message}");
            return new LicenseVerification(LicenseStatusCode.Malformed, reasons, null, envelope.KeyId, digest);
        }

        if (!string.Equals(document.Product, LicenseAuthority.ProductId, StringComparison.Ordinal))
        {
            reasons.Add($"This license was issued for '{document.Product}', not for '{LicenseAuthority.ProductId}'.");
            return new LicenseVerification(LicenseStatusCode.ProductMismatch, reasons, document, envelope.KeyId, digest);
        }

        var current = now ?? DateTimeOffset.UtcNow;
        if (document.ExpiresAt is { } expiry && current > expiry)
        {
            reasons.Add($"The license expired on {expiry.UtcDateTime:yyyy-MM-dd}.");
            return new LicenseVerification(LicenseStatusCode.Expired, reasons, document, envelope.KeyId, digest);
        }

        if (!string.IsNullOrWhiteSpace(document.Machine))
        {
            var expected = document.Machine.Trim();
            if (!string.Equals(expected, machineFingerprint.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("This license is bound to a different machine.");
                return new LicenseVerification(LicenseStatusCode.MachineMismatch, reasons, document, envelope.KeyId, digest);
            }
        }

        reasons.Add("The license is valid.");
        return new LicenseVerification(LicenseStatusCode.Valid, reasons, document, envelope.KeyId, digest);
    }

    /// <summary>Verifies a license file read from disk.</summary>
    public LicenseVerification VerifyFile(
        string path,
        string machineFingerprint,
        DateTimeOffset? now = null)
    {
        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (Exception exc)
        {
            return new LicenseVerification(
                LicenseStatusCode.Malformed,
                [$"The license file could not be read: {exc.Message}"],
                null,
                null,
                null);
        }

        LicenseEnvelope envelope;
        try
        {
            envelope = LicenseEnvelopeCodec.DecodeEnvelope(content);
        }
        catch (Exception exc)
        {
            return new LicenseVerification(
                LicenseStatusCode.Malformed,
                [$"The license file is not a valid envelope: {exc.Message}"],
                null,
                null,
                null);
        }

        return Verify(envelope, machineFingerprint, now);
    }
}

/// <summary>
/// Builds a stable, non-reversible fingerprint of the current machine for device binding.
/// </summary>
public static class MachineFingerprint
{
    /// <summary>Returns the fingerprint of the machine the process runs on.</summary>
    public static string Current()
    {
        var material = new StringBuilder()
            .Append(Environment.MachineName).Append('|')
            .Append(Environment.OSVersion.Platform).Append('|')
            .Append(Environment.UserName).Append('|')
            .Append(Environment.Is64BitProcess ? "x64" : "x86")
            .ToString();

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant()[..32];
    }
}
