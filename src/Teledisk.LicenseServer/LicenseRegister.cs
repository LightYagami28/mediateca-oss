using System.Text.Json;
using System.Text.Json.Serialization;

namespace Teledisk.LicenseServer;

/// <summary>
/// Subscription register served by the local license server.
/// </summary>
/// <remarks>
/// When the register is empty the server accepts every identity, which is the convenient
/// default for a single user. When it has entries, only listed identities are accepted and
/// the answer carries the registered expiry, so the client stores a real expiry date.
/// </remarks>
public sealed class LicenseRegister
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Identity entries, matched on the decrypted phone number or Telegram id.</summary>
    public List<LicenseEntry> Entries { get; init; } = [];

    /// <summary>Loads the register from disk, returning an empty (accept-all) register when absent.</summary>
    public static LicenseRegister Load(string path)
    {
        if (!File.Exists(path))
        {
            return new LicenseRegister();
        }

        try
        {
            return JsonSerializer.Deserialize<LicenseRegister>(File.ReadAllText(path), Options)
                   ?? new LicenseRegister();
        }
        catch (Exception)
        {
            return new LicenseRegister();
        }
    }

    /// <summary>Writes an example register so the operator has something to edit.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }

    /// <summary>Finds the entry matching an identity, or <c>null</c>.</summary>
    public LicenseEntry? Match(string? phone, string? telegramId)
    {
        if (Entries.Count == 0)
        {
            return new LicenseEntry { Subject = phone ?? telegramId ?? "any", AcceptAny = true };
        }

        foreach (var entry in Entries)
        {
            if (!string.IsNullOrWhiteSpace(phone)
                && string.Equals(Normalize(entry.Phone), Normalize(phone), StringComparison.Ordinal))
            {
                return entry;
            }

            if (!string.IsNullOrWhiteSpace(telegramId)
                && string.Equals(entry.TelegramId?.Trim(), telegramId.Trim(), StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : new string(value.Where(char.IsDigit).ToArray());
}

/// <summary>One registered identity.</summary>
public sealed class LicenseEntry
{
    /// <summary>Phone number, digits only are compared.</summary>
    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    /// <summary>Telegram user id as a decimal string.</summary>
    [JsonPropertyName("telegramId")]
    public string? TelegramId { get; init; }

    /// <summary>Tier granted to this identity. Ignored by the original client but useful in logs.</summary>
    [JsonPropertyName("tier")]
    public string Tier { get; init; } = "pro";

    /// <summary>Expiry written back to the client, <c>yyyy-MM-dd</c>.</summary>
    [JsonPropertyName("expires")]
    public string? Expires { get; init; }

    /// <summary>Free-form note.</summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }

    /// <summary>Set when this entry is the implicit accept-all fallback.</summary>
    [JsonIgnore]
    public bool AcceptAny { get; init; }

    /// <summary>Human readable subject used in logs.</summary>
    [JsonIgnore]
    public string Subject { get; init; } = "";
}
