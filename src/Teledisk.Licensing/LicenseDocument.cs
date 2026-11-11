using System.Text.Json.Serialization;

namespace Teledisk.Licensing;

/// <summary>
/// The signed content of a Teledisk license.
/// </summary>
/// <remarks>
/// The payload is serialised with a fixed property order and no insignificant whitespace,
/// so the exact bytes can be reproduced by any implementation and re-verified offline.
/// </remarks>
public sealed record LicenseDocument
{
    /// <summary>Stable identifier of this license, used for revocation lists and audit logs.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Product identifier the license was issued for.</summary>
    [JsonPropertyName("product")]
    public string Product { get; init; } = LicenseAuthority.ProductId;

    /// <summary>Entitlement tier granted by this license.</summary>
    [JsonPropertyName("tier")]
    public LicenseTier Tier { get; init; } = LicenseTier.Community;

    /// <summary>Optional capabilities granted in addition to the tier defaults.</summary>
    [JsonPropertyName("features")]
    public LicenseFeatures Features { get; init; } = LicenseFeatures.None;

    /// <summary>Subject the license was issued to, for example an e-mail address.</summary>
    [JsonPropertyName("subject")]
    public string Subject { get; init; } = "";

    /// <summary>Issue timestamp, in UTC.</summary>
    [JsonPropertyName("issuedAt")]
    public DateTimeOffset IssuedAt { get; init; }

    /// <summary>Expiry timestamp, in UTC. <c>null</c> means perpetual.</summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Machine fingerprint this license is bound to. Empty means the license is portable.</summary>
    [JsonPropertyName("machine")]
    public string Machine { get; init; } = "";

    /// <summary>Maximum number of distinct machines allowed to use this license. Zero means unlimited.</summary>
    [JsonPropertyName("maxMachines")]
    public int MaxMachines { get; init; }

    /// <summary>Free-form note, for example the order reference.</summary>
    [JsonPropertyName("note")]
    public string Note { get; init; } = "";

    /// <summary>True when the license never expires.</summary>
    [JsonIgnore]
    public bool IsPerpetual => ExpiresAt is null;
}
