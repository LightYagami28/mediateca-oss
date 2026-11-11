using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Teledisk.Licensing;

/// <summary>
/// Signed container that carries a <see cref="LicenseDocument"/>.
/// </summary>
/// <remarks>
/// The envelope is a small JSON object: the canonical payload bytes (base64url), the
/// detached signature (base64url) and the identifier of the key that produced it.
/// Verification only needs the public key; issuing needs the private key, which never
/// belongs in this repository.
/// </remarks>
public sealed record LicenseEnvelope
{
    /// <summary>Envelope format version.</summary>
    [JsonPropertyName("v")]
    public int Version { get; init; } = 1;

    /// <summary>Identifier of the signing key, so several keys can be trusted at once.</summary>
    [JsonPropertyName("kid")]
    public required string KeyId { get; init; }

    /// <summary>Signature algorithm. Only <c>Ed25519</c> is accepted.</summary>
    [JsonPropertyName("alg")]
    public string Algorithm { get; init; } = LicenseSigner.AlgorithmName;

    /// <summary>Base64url encoded canonical payload bytes.</summary>
    [JsonPropertyName("payload")]
    public required string Payload { get; init; }

    /// <summary>Base64url encoded detached signature over the payload bytes.</summary>
    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}

/// <summary>
/// Encodes and decodes <see cref="LicenseEnvelope"/> instances.
/// </summary>
public static class LicenseEnvelopeCodec
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Serialises a document to the canonical payload bytes that get signed.</summary>
    public static byte[] EncodePayload(LicenseDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var json = JsonSerializer.Serialize(document, PayloadOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>Deserialises a document from canonical payload bytes.</summary>
    public static LicenseDocument DecodePayload(ReadOnlySpan<byte> payload)
    {
        var document = JsonSerializer.Deserialize<LicenseDocument>(payload);
        return document ?? throw new FormatException("The license payload is not a valid document.");
    }

    /// <summary>Serialises an envelope to its on-disk JSON representation.</summary>
    public static string EncodeEnvelope(LicenseEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Parses an envelope from its on-disk JSON representation.</summary>
    public static LicenseEnvelope DecodeEnvelope(string json)
    {
        var envelope = JsonSerializer.Deserialize<LicenseEnvelope>(json);
        return envelope ?? throw new FormatException("The license file is not a valid envelope.");
    }

    /// <summary>Base64url encoding without padding, safe for file names and JSON.</summary>
    public static string ToBase64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Decodes base64url text, tolerating missing padding.</summary>
    public static byte[] FromBase64Url(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }

    /// <summary>Computes the SHA-256 of the payload, used as the document digest for audit logs.</summary>
    public static string PayloadDigest(ReadOnlySpan<byte> payload) =>
        Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
}
