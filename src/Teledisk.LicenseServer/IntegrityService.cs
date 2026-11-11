using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Teledisk.LicenseServer;

/// <summary>
/// Answers the integrity endpoint used by the client launcher.
/// </summary>
/// <remarks>
/// Reconstructed from the shipped launcher, which posts the signed manifest it holds in
/// <c>_internal/integrity_control.json</c> and expects:
/// <code>
/// 200 {"integrity": true, "status": "active", "current_version": "1.7", "latest_version": "1.7", ...}
/// </code>
/// The client accepts only <c>status</c> active or inactive and treats a missing or non-200
/// answer as "server di verifica non raggiungibile".
/// </remarks>
public sealed class IntegrityService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string _appDirectory;
    private readonly Action<string> _log;

    /// <summary>Creates the service for an installed client directory.</summary>
    public IntegrityService(string appDirectory, Action<string> log)
    {
        _appDirectory = appDirectory;
        _log = log;
    }

    /// <summary>Manifest shipped by the client, used as the comparison baseline.</summary>
    private string ManifestPath => Path.Combine(_appDirectory, "_internal", "integrity_control.json");

    /// <summary>Version file shipped by the client.</summary>
    private string VersionPath => Path.Combine(_appDirectory, "_internal", "backend", "v.json");

    /// <summary>Version the server reports as current.</summary>
    public string CurrentVersion()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(VersionPath));
            return document.RootElement.TryGetProperty("version", out var version)
                ? version.GetString() ?? "1.7"
                : "1.7";
        }
        catch (Exception)
        {
            return "1.7";
        }
    }

    /// <summary>Handles one integrity request.</summary>
    public (int Status, string Body) Handle(byte[] body)
    {
        var mismatched = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var clientVersion = root.TryGetProperty("version", out var v) ? v.GetString() : null;
            var os = root.TryGetProperty("os", out var o) ? o.GetString() : null;
            _log($"  client version={clientVersion ?? "-"} os={os ?? "-"}");

            if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in files.EnumerateObject())
                {
                    var expected = entry.Value.TryGetProperty("hash", out var h) ? h.GetString() : null;
                    if (string.IsNullOrWhiteSpace(expected))
                    {
                        continue;
                    }

                    var actual = HashFile(entry.Name);
                    if (actual is null)
                    {
                        mismatched.Add($"{entry.Name}: assente");
                    }
                    else if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        mismatched.Add($"{entry.Name}: {actual[..12]} != {expected[..12]}");
                    }
                }
            }
        }
        catch (JsonException exc)
        {
            _log($"  manifest non leggibile: {exc.Message}");
        }

        var integrity = mismatched.Count == 0;
        foreach (var line in mismatched)
        {
            _log($"  discrepanza -> {line}");
        }

        var response = new IntegrityResponse
        {
            Integrity = integrity,
            Status = "active",
            Active = true,
            CurrentVersion = CurrentVersion(),
            CurrentLink = "",
            LatestVersion = CurrentVersion(),
            Link = "",
            Message = integrity ? "" : "Dati di integrità non corrispondenti.",
        };

        _log($"  -> integrity={integrity} status=active versione={response.CurrentVersion}");
        return (200, JsonSerializer.Serialize(response, Json));
    }

    private string? HashFile(string relativePath)
    {
        var path = Path.Combine(_appDirectory, "_internal", relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed class IntegrityResponse
    {
        [JsonPropertyName("integrity")]
        public bool Integrity { get; init; }

        [JsonPropertyName("status")]
        public string Status { get; init; } = "active";

        [JsonPropertyName("active")]
        public bool Active { get; init; }

        [JsonPropertyName("current_version")]
        public string CurrentVersion { get; init; } = "";

        [JsonPropertyName("current_link")]
        public string CurrentLink { get; init; } = "";

        [JsonPropertyName("latest_version")]
        public string LatestVersion { get; init; } = "";

        [JsonPropertyName("link")]
        public string Link { get; init; } = "";

        [JsonPropertyName("message")]
        public string Message { get; init; } = "";
    }
}
