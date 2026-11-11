using System.Text.Json;

namespace Teledisk.Licensing;

/// <summary>
/// Resolves the effective entitlement of the running installation.
/// </summary>
/// <remarks>
/// No license file is required to run: <see cref="LicenseTier.Community"/> is the
/// always-available baseline. A license file only adds optional capabilities, so a
/// misconfigured or expired license never bricks the application.
/// </remarks>
public sealed class LicenseAuthority
{
    /// <summary>Product identifier written into every license.</summary>
    public const string ProductId = "teledisk";

    /// <summary>Environment variable that overrides the license file location.</summary>
    public const string LicensePathVariable = "TELEDISK_LICENSE_PATH";

    /// <summary>Environment variable that overrides the data directory.</summary>
    public const string HomeVariable = "TELEDISK_HOME";

    /// <summary>Default file name of the license.</summary>
    public const string LicenseFileName = "license" + LicenseSigner.LicenseFileExtension;

    private readonly LicenseVerifier _verifier;
    private readonly Func<string> _machineFingerprint;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>Creates an authority that verifies against the supplied trusted keys.</summary>
    public LicenseAuthority(
        LicenseVerifier verifier,
        Func<string>? machineFingerprint = null,
        Func<DateTimeOffset>? clock = null)
    {
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _machineFingerprint = machineFingerprint ?? MachineFingerprint.Current;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>Creates an authority trusting the embedded project key and any locally trusted keys.</summary>
    public static LicenseAuthority CreateDefault(string? appDirectory = null) =>
        new(new LicenseVerifier(TrustedKeys.Load(appDirectory)));

    /// <summary>Directory that holds user data (settings, database, license).</summary>
    public static string ResolveDataDirectory()
    {
        var custom = Environment.GetEnvironmentVariable(HomeVariable);
        if (!string.IsNullOrWhiteSpace(custom))
        {
            return Path.GetFullPath(custom);
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        return Path.Combine(root, "Teledisk");
    }

    /// <summary>Candidate locations searched for a license file, in priority order.</summary>
    public static IEnumerable<string> LicenseCandidates(string? appDirectory = null)
    {
        var explicitPath = Environment.GetEnvironmentVariable(LicensePathVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            yield return Path.GetFullPath(explicitPath);
        }

        yield return Path.Combine(ResolveDataDirectory(), LicenseFileName);

        if (!string.IsNullOrWhiteSpace(appDirectory))
        {
            yield return Path.Combine(appDirectory, LicenseFileName);
        }
    }

    /// <summary>Resolves the current entitlement, never throwing.</summary>
    public LicenseStatus Resolve(string? appDirectory = null)
    {
        var fingerprint = _machineFingerprint();

        foreach (var candidate in LicenseCandidates(appDirectory))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var verification = _verifier.VerifyFile(candidate, fingerprint, _clock());
            return LicenseStatus.From(candidate, verification);
        }

        return LicenseStatus.Community("No license file found; running with the full Community feature set.");
    }

    /// <summary>True when the current installation may use the supplied capability.</summary>
    public bool Allows(LicenseFeatures feature, string? appDirectory = null) =>
        Resolve(appDirectory).Features.HasFlag(feature);

    /// <summary>
    /// Funzionalità concesse da un tier.
    /// </summary>
    /// <remarks>
    /// Il progetto è pubblico al 100%: <b>ogni tier riceve tutte le funzionalità</b>. Non esiste
    /// una chiave obbligatoria e non esiste una funzione bloccata: un file di licenza, se c'è,
    /// serve solo a dare un'identità e una scadenza all'installazione e a comparire nei log.
    /// Il parametro resta per compatibilità se in futuro un tier dovrà differire.
    /// </remarks>
    public static LicenseFeatures DefaultFeatures(LicenseTier tier) => LicenseFeatures.All;
}

/// <summary>
/// Effective entitlement of an installation.
/// </summary>
/// <param name="Tier">Granted tier.</param>
/// <param name="Features">Granted capabilities.</param>
/// <param name="Source">License file the entitlement comes from, when any.</param>
/// <param name="Subject">License subject, when a license was found.</param>
/// <param name="ExpiresAt">Expiry of the license, when applicable.</param>
/// <param name="Code">Verification outcome.</param>
/// <param name="Reasons">Human readable explanation, suitable for logs and CLI output.</param>
public sealed record LicenseStatus(
    LicenseTier Tier,
    LicenseFeatures Features,
    string? Source,
    string? Subject,
    DateTimeOffset? ExpiresAt,
    LicenseStatusCode Code,
    IReadOnlyList<string> Reasons)
{
    /// <summary>True when a valid, non-expired license was found.</summary>
    public bool HasLicense => Code == LicenseStatusCode.Valid;

    /// <summary>True when the installation is running on the free baseline.</summary>
    public bool IsCommunity => !HasLicense || Tier == LicenseTier.Community;

    internal static LicenseStatus From(string source, LicenseVerification verification)
    {
        if (!verification.IsUsable || verification.Document is null)
        {
            // Fall back to Community: a bad license degrades gracefully instead of blocking the app.
            var reasons = verification.Reasons.Concat(["Falling back to the Community feature set."]).ToArray();
            return new LicenseStatus(
                LicenseTier.Community,
                LicenseAuthority.DefaultFeatures(LicenseTier.Community),
                source,
                verification.Document?.Subject,
                verification.Document?.ExpiresAt,
                verification.Code,
                reasons);
        }

        var features = LicenseAuthority.DefaultFeatures(verification.Document.Tier) | verification.Document.Features;
        return new LicenseStatus(
            verification.Document.Tier,
            features,
            source,
            verification.Document.Subject,
            verification.Document.ExpiresAt,
            verification.Code,
            verification.Reasons);
    }

    internal static LicenseStatus Community(string reason) => new(
        LicenseTier.Community,
        LicenseAuthority.DefaultFeatures(LicenseTier.Community),
        null,
        null,
        null,
        LicenseStatusCode.NotFound,
        [reason]);

    /// <summary>Serialises the status for CLI output.</summary>
    public string ToJson() => JsonSerializer.Serialize(new
    {
        tier = Tier.ToString(),
        features = Features.ToString(),
        hasLicense = HasLicense,
        source = Source,
        subject = Subject,
        expiresAt = ExpiresAt,
        code = Code.ToString(),
        reasons = Reasons,
    }, new JsonSerializerOptions { WriteIndented = true });
}
