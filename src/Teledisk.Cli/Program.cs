using System.Globalization;
using System.Text;
using Teledisk.Licensing;

namespace Teledisk.Cli;

/// <summary>
/// Command line entry point of Teledisk Community.
/// </summary>
/// <remarks>
/// Deliberately dependency free: the whole surface is plain argument parsing, so the CLI can be
/// published as a single trimmed binary and used on machines without a package manager.
/// </remarks>
public static class Program
{
    private const string Usage = """
        teledisk — Teledisk Community command line

        USAGE
          teledisk <command> [options]

        COMMANDS
          machine                     print the fingerprint of this machine
          doctor                      environment and entitlement report
          db                          create or update the database
          settings [show|set|password] read or change the user settings
          scan <directory>            catalogue a local directory
          backup [--keep N]           zip the data directory into backups
          serve [--port N] [--bind A] start the HTTP/WebDAV server

          license status              show the effective entitlement of this installation
          license verify <file>       verify a license file without installing it
          license keygen --out <dir>  create a new Ed25519 issuer key pair
          license issue [options]     issue a signed license file
          license install <file>      copy a license into the data directory

        LICENSE ISSUE OPTIONS
          --key <private.pem>   issuer private key (default: first *private.pem in <data>/keys)
          --tier <community|pro|patron>            default: pro
          --subject <text>      licensee, usually an e-mail address
          --expires <yyyy-MM-dd>                   default: perpetual
          --machine <fingerprint|this>             default: portable
          --features <a,b,c>    extra features on top of the tier defaults
          --note <text>         free-form note
          --id <text>           explicit license id
          --out <file>                             default: <id>.tlicense

        EXIT CODES
          0 success · 1 usage error · 2 verification failed
        """;

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "machine" => Machine(),
                "doctor" => Doctor(),
                "license" => License(args[1..]),
                "db" => args.Length > 1 && args[1] == "info" ? Commands.DatabaseInit() : Commands.DatabaseInit(),
                "settings" => Commands.Settings(args[1..]),
                "scan" => Commands.Scan(args[1..]),
                "backup" => Commands.Backup(args[1..]),
                "serve" => Commands.Serve(args[1..]).GetAwaiter().GetResult(),
                _ => Fail($"Unknown command '{args[0]}'."),
            };
        }
        catch (UsageException exc)
        {
            Console.Error.WriteLine($"error: {exc.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage);
            return 1;
        }
    }

    private static int Machine()
    {
        Console.WriteLine(MachineFingerprint.Current());
        return 0;
    }

    private static int Doctor()
    {
        var authority = LicenseAuthority.CreateDefault(AppContext.BaseDirectory);
        var status = authority.Resolve(AppContext.BaseDirectory);

        Console.WriteLine("teledisk doctor");
        Console.WriteLine($"  os            : {Environment.OSVersion}");
        Console.WriteLine($"  runtime       : {Environment.Version}");
        Console.WriteLine($"  machine       : {MachineFingerprint.Current()}");
        Console.WriteLine($"  data dir      : {LicenseAuthority.ResolveDataDirectory()}");
        Console.WriteLine($"  license path  : {Environment.GetEnvironmentVariable(LicenseAuthority.LicensePathVariable) ?? "(not set)"}");
        Console.WriteLine($"  trusted keys  : {TrustedKeys.Load(AppContext.BaseDirectory).Count}");
        Console.WriteLine($"  tier          : {status.Tier}");
        Console.WriteLine($"  features      : {status.Features}");
        Console.WriteLine($"  license state : {status.Code}");
        foreach (var reason in status.Reasons)
        {
            Console.WriteLine($"  note          : {reason}");
        }

        return status.Code is LicenseStatusCode.Valid or LicenseStatusCode.NotFound ? 0 : 2;
    }

    private static int License(string[] args)
    {
        if (args.Length == 0)
        {
            throw new UsageException("license requires a subcommand: status, verify, keygen, issue, install.");
        }

        return args[0] switch
        {
            "status" => LicenseStatusCommand(),
            "verify" => LicenseVerify(args[1..]),
            "keygen" => LicenseKeygen(args[1..]),
            "issue" => LicenseIssue(args[1..]),
            "install" => LicenseInstall(args[1..]),
            _ => throw new UsageException($"unknown license subcommand '{args[0]}'."),
        };
    }

    private static int LicenseStatusCommand()
    {
        var status = LicenseAuthority.CreateDefault(AppContext.BaseDirectory).Resolve(AppContext.BaseDirectory);
        Console.WriteLine(status.ToJson());
        return status.HasLicense ? 0 : 2;
    }

    private static int LicenseVerify(string[] args)
    {
        var path = Require(args, 0, "license file");
        var fingerprint = MachineFingerprint.Current();
        var verifier = new LicenseVerifier(TrustedKeys.Load(AppContext.BaseDirectory));
        var result = verifier.VerifyFile(path, fingerprint);

        Console.WriteLine($"state   : {result.Code}");
        Console.WriteLine($"key     : {result.KeyId ?? "(unknown)"}");
        Console.WriteLine($"digest  : {result.PayloadDigest ?? "(none)"}");
        if (result.Document is { } document)
        {
            Console.WriteLine($"tier    : {document.Tier}");
            Console.WriteLine($"subject : {document.Subject}");
            Console.WriteLine($"expires : {(document.ExpiresAt?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "never")}");
            Console.WriteLine($"machine : {(string.IsNullOrWhiteSpace(document.Machine) ? "portable" : document.Machine)}");
        }

        foreach (var reason in result.Reasons)
        {
            Console.WriteLine($"note    : {reason}");
        }

        return result.IsUsable ? 0 : 2;
    }

    private static int LicenseKeygen(string[] args)
    {
        var outDir = Option(args, "--out") ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outDir);

        var (privatePem, publicPem, keyId) = LicenseSigner.GenerateKeyPair();
        var privatePath = Path.Combine(outDir, $"teledisk-{keyId}-private.pem");
        var publicPath = Path.Combine(outDir, $"teledisk-{keyId}.pub.pem");

        File.WriteAllText(privatePath, privatePem);
        File.WriteAllText(publicPath, publicPem);

        Console.WriteLine($"key id       : {keyId}");
        Console.WriteLine($"private key  : {privatePath}   (keep outside version control)");
        Console.WriteLine($"public key   : {publicPath}");
        Console.WriteLine();
        Console.WriteLine("Trust it locally by copying the public key into:");
        Console.WriteLine($"  {Path.Combine(LicenseAuthority.ResolveDataDirectory(), "keys", "trusted")}");

        return 0;
    }

    private static int LicenseIssue(string[] args)
    {
        var keyPath = Option(args, "--key") ?? DefaultIssuerKey();
        if (!File.Exists(keyPath))
        {
            throw new UsageException($"private key not found: {keyPath}");
        }

        var tier = Option(args, "--tier")?.ToLowerInvariant() switch
        {
            null or "pro" => LicenseTier.Pro,
            "community" => LicenseTier.Community,
            "patron" => LicenseTier.Patron,
            var other => throw new UsageException($"unknown tier '{other}'."),
        };

        var features = LicenseFeatures.None;
        if (Option(args, "--features") is { } rawFeatures)
        {
            foreach (var name in rawFeatures.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<LicenseFeatures>(name, ignoreCase: true, out var parsed))
                {
                    throw new UsageException($"unknown feature '{name}'.");
                }

                features |= parsed;
            }
        }

        DateTimeOffset? expires = null;
        if (Option(args, "--expires") is { } rawExpiry)
        {
            if (!DateOnly.TryParse(rawExpiry, CultureInfo.InvariantCulture, out var date))
            {
                throw new UsageException($"--expires must be yyyy-MM-dd, got '{rawExpiry}'.");
            }

            expires = new DateTimeOffset(date.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        }

        var machine = Option(args, "--machine") switch
        {
            null or "portable" => "",
            "this" => MachineFingerprint.Current(),
            var explicitFingerprint => explicitFingerprint,
        };

        var id = Option(args, "--id") ?? $"tl-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8]}";
        var document = new LicenseDocument
        {
            Id = id,
            Tier = tier,
            Features = features,
            Subject = Option(args, "--subject") ?? "",
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expires,
            Machine = machine,
            Note = Option(args, "--note") ?? "",
        };

        var privatePem = File.ReadAllText(keyPath);
        var keyId = LicenseSigner.ComputeKeyId(DerivePublicKey(privatePem));
        var content = LicenseSigner.IssueToFile(document, privatePem, keyId);
        var outPath = Option(args, "--out") ?? Path.Combine(Directory.GetCurrentDirectory(), id + LicenseSigner.LicenseFileExtension);

        File.WriteAllText(outPath, content);

        Console.WriteLine($"license      : {outPath}");
        Console.WriteLine($"id           : {id}");
        Console.WriteLine($"tier         : {tier}");
        Console.WriteLine($"features     : {(features == LicenseFeatures.None ? "(tier defaults)" : features.ToString())}");
        Console.WriteLine($"subject      : {(string.IsNullOrWhiteSpace(document.Subject) ? "(none)" : document.Subject)}");
        Console.WriteLine($"expires      : {(expires?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "never")}");
        Console.WriteLine($"machine      : {(string.IsNullOrWhiteSpace(machine) ? "portable" : machine)}");
        Console.WriteLine($"key id       : {keyId}");

        return 0;
    }

    private static int LicenseInstall(string[] args)
    {
        var source = Require(args, 0, "license file");
        var target = Path.Combine(LicenseAuthority.ResolveDataDirectory(), LicenseAuthority.LicenseFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);

        Console.WriteLine($"installed : {target}");
        return LicenseStatusCommand();
    }

    /// <summary>
    /// Chiave privata dell'issuer da usare quando <c>--key</c> non è indicata: la prima
    /// <c>*private.pem</c> nella cartella <c>keys</c> dell'installazione.
    /// </summary>
    private static string DefaultIssuerKey()
    {
        var directory = Path.Combine(LicenseAuthority.ResolveDataDirectory(), "keys");
        if (Directory.Exists(directory))
        {
            var candidate = Directory.EnumerateFiles(directory, "*private.pem", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is not null)
            {
                return candidate;
            }
        }

        throw new UsageException(
            $"nessuna chiave privata in '{directory}': usa --key <file> oppure esegui 'teledisk license keygen'");
    }

    /// <summary>Derives the public key from a PKCS#8 private key so the key id can be computed.</summary>
    private static string DerivePublicKey(string privateKeyPem)
    {
        using var rsa = System.Security.Cryptography.RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return rsa.ExportSubjectPublicKeyInfoPem();
    }

    private static string Require(string[] args, int index, string what) =>
        args.Length > index && !args[index].StartsWith("--", StringComparison.Ordinal)
            ? args[index]
            : throw new UsageException($"missing argument: {what}.");

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 1;
    }

    /// <summary>Raised when the command line is malformed.</summary>
    public sealed class UsageException(string message) : Exception(message);
}
