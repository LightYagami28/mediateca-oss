using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Teledisk.LicenseServer;

/// <summary>
/// Answers the activation endpoint used by the original desktop client.
/// </summary>
/// <remarks>
/// Protocol reconstructed from the shipped client:
/// <code>
/// POST https://teledisk.net/authorizzation/key-access
/// {"client":"desktop","contract_version":2,"phone":"&lt;rsa pkcs1v15 base64&gt;","telegram_id":"..."}
/// </code>
/// The client encrypts the identity with the RSA <em>public</em> key and trusts the JSON answer:
/// a 200 response with <c>{"success":true,"key":"...","expired":"..."}</c> is the whole contract.
/// No signature is verified on the client, so a locally served answer is indistinguishable
/// from the vendor one as far as the application is concerned.
/// </remarks>
public sealed class ActivationService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly RSA? _privateKey;
    private readonly LicenseRegister _register;
    private readonly IntegrityService? _integrity;
    private readonly Action<string> _log;

    /// <summary>Creates the service.</summary>
    /// <param name="privateKey">Private key used to decrypt identities, or <c>null</c> when accepting everything blindly.</param>
    /// <param name="register">Identity register; empty means accept any identity.</param>
    /// <param name="log">Log sink.</param>
    /// <param name="integrity">Optional integrity endpoint handler.</param>
    public ActivationService(RSA? privateKey, LicenseRegister register, Action<string> log, IntegrityService? integrity = null)
    {
        _privateKey = privateKey;
        _register = register;
        _log = log;
        _integrity = integrity;
    }

    /// <summary>Path handled by the activation endpoint.</summary>
    public const string ActivationPath = "/authorizzation/key-access";

    /// <summary>Path handled by the integrity endpoint.</summary>
    public const string IntegrityPath = "/authorizzation/test-integrity-check";

    /// <summary>Handles one request and returns status code plus JSON body.</summary>
    public (int Status, string Body) Handle(string path, byte[] body)
    {
        if (path.StartsWith(IntegrityPath, StringComparison.OrdinalIgnoreCase))
        {
            return _integrity is null
                ? (200, """{"integrity":true,"status":"active"}""")
                : _integrity.Handle(body);
        }

        if (!path.StartsWith(ActivationPath, StringComparison.OrdinalIgnoreCase))
        {
            return (404, """{"success":false,"code":"not_found"}""");
        }

        ActivationRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize<ActivationRequest>(body);
        }
        catch (JsonException exc)
        {
            _log($"  request body is not JSON: {exc.Message}");
        }

        var phone = Decrypt(request?.Phone);
        var telegram = Decrypt(request?.TelegramId);
        _log($"  client={request?.Client ?? "-"} contract={request?.ContractVersion?.ToString() ?? "-"} phone={Mask(phone)} telegram={telegram ?? "-"}");

        var entry = _register.Match(phone, telegram);
        if (entry is null)
        {
            _log("  -> refused: identity not in the register");
            return (200, """{"success":false,"code":"not_registered"}""");
        }

        var expires = string.IsNullOrWhiteSpace(entry.Expires)
            ? DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd")
            : entry.Expires;

        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{phone}|{telegram}|{expires}")))
            .ToLowerInvariant()[..32];

        _log($"  -> granted tier={entry.Tier} expires={expires}");

        var payload = new ActivationResponse
        {
            Success = true,
            Key = token,
            Expired = expires,
        };

        return (200, JsonSerializer.Serialize(payload, Json));
    }

    private string? Decrypt(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64) || _privateKey is null)
        {
            return null;
        }

        try
        {
            var cipher = Convert.FromBase64String(base64);
            var plain = _privateKey.Decrypt(cipher, RSAEncryptionPadding.Pkcs1);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            // The client was built against a different public key: the identity stays opaque.
            return null;
        }
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Length <= 4 ? value : new string('*', value.Length - 4) + value[^4..];
    }

    private sealed class ActivationRequest
    {
        [JsonPropertyName("client")]
        public string? Client { get; init; }

        [JsonPropertyName("contract_version")]
        public int? ContractVersion { get; init; }

        [JsonPropertyName("phone")]
        public string? Phone { get; init; }

        [JsonPropertyName("telegram_id")]
        public string? TelegramId { get; init; }
    }

    private sealed class ActivationResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("expired")]
        public string? Expired { get; init; }
    }
}
