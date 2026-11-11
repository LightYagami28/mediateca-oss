using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Teledisk.LicenseServer;

namespace Teledisk.LicenseServer;

/// <summary>
/// Local activation server for the original Teledisk desktop client.
/// </summary>
/// <remarks>
/// Reconstructed from the shipped client, which encrypts the identity with an RSA public key,
/// posts it to <c>https://teledisk.net/authorizzation/key-access</c> and trusts the JSON answer.
/// This tool serves that endpoint from the local machine, so the client can be activated
/// without the vendor service and without touching the hosts file.
/// </remarks>
public static class Program
{
    private const string Usage = """
        teledisk-license-server — local activation server for the Teledisk desktop client

        USAGE
          teledisk-license-server <command> [options]

        COMMANDS
          keygen            create the RSA key pair used to encrypt/decrypt identities
          cert              create the TLS certificate for teledisk.net
          trust --app <dir> add that certificate to the client trust store (certifi bundle)
          register          create or extend the identity register (licenses.json)
          serve             start the proxy that answers the activation endpoint
          launcher --app <dir> --out <file>   write a .cmd that starts the client activated
          status            show paths, certificate and register summary

        KEY OPTIONS
          --home <dir>      working directory (default: %APPDATA%\Teledisk\license-server)
          --port <n>        proxy port (default: 8443)
          --allow-any       accept every identity, ignoring the register

        HOW THE CLIENT IS ACTIVATED
          1. run "cert" and "trust --app <client dir>"
          2. run "serve"
          3. start the client with HTTPS_PROXY=http://127.0.0.1:<port>  (the "launcher" command writes this)
        """;

    private static string Home =>
        Environment.GetEnvironmentVariable("TELEDISK_LICENSE_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Teledisk", "license-server");

    private static string PrivateKeyPath => Path.Combine(Home, "issuer-private.pem");
    private static string PublicKeyPath => Path.Combine(Home, "pubblic.pem");
    private static string PublicKeyPathAlt => Path.Combine(Home, "public.pem");
    private static string CertificatePfxPath => Path.Combine(Home, "teledisk-net.pfx");
    private static string CertificatePemPath => Path.Combine(Home, "teledisk-net.pem");
    private static string RegisterPath => Path.Combine(Home, "licenses.json");

    private static void Log(string message)
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");
    }

    /// <summary>Entry point.</summary>
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
                "keygen" => Keygen(args[1..]),
                "cert" => Certificate(),
                "trust" => Trust(args[1..]),
                "register" => Register(args[1..]),
                "serve" => Serve(args[1..]),
                "launcher" => Launcher(args[1..]),
                "status" => Status(),
                _ => Fail($"unknown command '{args[0]}'"),
            };
        }
        catch (Exception exc)
        {
            return Fail(exc.Message);
        }
    }

    private static int Keygen(string[] args)
    {
        Directory.CreateDirectory(Home);
        if (File.Exists(PrivateKeyPath) && !args.Contains("--force"))
        {
            Console.WriteLine($"key pair already present: {PrivateKeyPath}");
            Console.WriteLine("pass --force to regenerate");
            return 0;
        }

        using var rsa = RSA.Create(2048);
        File.WriteAllText(PrivateKeyPath, rsa.ExportPkcs8PrivateKeyPem());
        // The client looks for exactly these two names next to the application.
        File.WriteAllText(PublicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        File.WriteAllText(PublicKeyPathAlt, rsa.ExportSubjectPublicKeyInfoPem());

        Console.WriteLine($"private key : {PrivateKeyPath}");
        Console.WriteLine($"public key  : {PublicKeyPath} (and {Path.GetFileName(PublicKeyPathAlt)})");
        Console.WriteLine();
        Console.WriteLine("The client must encrypt with THIS public key for identities to be readable by the server.");
        Console.WriteLine("Point it there with TELEDISK_RES_DIR (see the launcher command) or by copying the two files");
        Console.WriteLine("over pubblic.pem / public.pem next to teledisk.exe.");
        return 0;
    }

    private static int Certificate()
    {
        Directory.CreateDirectory(Home);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=teledisk.net", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("teledisk.net");
        san.AddDnsName("www.teledisk.net");
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));
        File.WriteAllBytes(CertificatePfxPath, certificate.Export(X509ContentType.Pfx));
        File.WriteAllText(CertificatePemPath, certificate.ExportCertificatePem());

        Console.WriteLine($"certificate : {CertificatePemPath}");
        Console.WriteLine($"pfx         : {CertificatePfxPath}");
        Console.WriteLine($"thumbprint  : {certificate.Thumbprint}");
        Console.WriteLine($"valid until : {certificate.NotAfter:yyyy-MM-dd}");
        return 0;
    }

    private static int Trust(string[] args)
    {
        var appDirectory = Option(args, "--app") ?? throw new UsageException("--app <client directory> is required");
        var bundle = Path.Combine(appDirectory, "_internal", "certifi", "cacert.pem");
        if (!File.Exists(bundle))
        {
            bundle = Path.Combine(appDirectory, "certifi", "cacert.pem");
        }

        if (!File.Exists(bundle))
        {
            throw new UsageException($"certifi bundle not found under '{appDirectory}'");
        }

        var backup = bundle + ".bak";
        if (!File.Exists(backup))
        {
            File.Copy(bundle, backup);
            Console.WriteLine($"backup      : {backup}");
        }

        var pem = File.ReadAllText(CertificatePemPath);
        var current = File.ReadAllText(bundle);
        var marker = "# teledisk-license-server";

        if (current.Contains(marker, StringComparison.Ordinal))
        {
            var start = current.IndexOf(marker, StringComparison.Ordinal);
            current = current[..start].TrimEnd() + Environment.NewLine;
        }

        File.WriteAllText(bundle, current.TrimEnd() + Environment.NewLine + marker + Environment.NewLine + pem + Environment.NewLine);
        Console.WriteLine($"trusted     : {bundle}");
        Console.WriteLine($"entries     : {current.Count(c => c == '-')} certificates before, marker added");
        return 0;
    }

    private static int Register(string[] args)
    {
        var register = LicenseRegister.Load(RegisterPath);

        if (args.Length == 0 || args[0] == "show")
        {
            Console.WriteLine(RegisterPath);
            Console.WriteLine(register.Entries.Count == 0
                ? "empty: every identity is accepted"
                : $"{register.Entries.Count} entr{(register.Entries.Count == 1 ? "y" : "ies")}");
            foreach (var entry in register.Entries)
            {
                Console.WriteLine($"  {entry.Phone ?? entry.TelegramId} tier={entry.Tier} expires={entry.Expires ?? "-"} {entry.Note}");
            }

            return 0;
        }

        if (args[0] == "add")
        {
            var phone = Option(args, "--phone");
            var telegram = Option(args, "--telegram");
            if (phone is null && telegram is null)
            {
                throw new UsageException("give --phone and/or --telegram");
            }

            register.Entries.Add(new LicenseEntry
            {
                Phone = phone,
                TelegramId = telegram,
                Tier = Option(args, "--tier") ?? "pro",
                Expires = Option(args, "--expires"),
                Note = Option(args, "--note"),
            });

            register.Save(RegisterPath);
            Console.WriteLine($"register    : {RegisterPath} ({register.Entries.Count} entries)");
            return 0;
        }

        throw new UsageException($"unknown register subcommand '{args[0]}'");
    }

    private static int Serve(string[] args)
    {
        var port = int.TryParse(Option(args, "--port"), out var parsed) ? parsed : 8443;
        var allowAny = args.Contains("--allow-any");

        var register = allowAny ? new LicenseRegister() : LicenseRegister.Load(RegisterPath);

        RSA? privateKey = null;
        if (File.Exists(PrivateKeyPath))
        {
            privateKey = RSA.Create();
            privateKey.ImportFromPem(File.ReadAllText(PrivateKeyPath));
            Log($"private key loaded, identities will be decrypted and matched");
        }
        else
        {
            Log("no private key: identities stay opaque and matching falls back to accept-all");
        }

        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(CertificatePfxPath, password: null);
        Log($"certificate: {certificate.Subject} until {certificate.NotAfter:yyyy-MM-dd}");

        var appDirectory = Option(args, "--app") ?? DetectAppDirectory();
        var integrity = appDirectory is null ? null : new IntegrityService(appDirectory, Log);
        if (appDirectory is not null)
        {
            Log($"client directory: {appDirectory} (verifica integrità attiva)");
        }

        var service = new ActivationService(privateKey, register, Log, integrity);
        var proxy = new ActivationProxy(service, certificate, Log);
        proxy.Start(port);

        Log($"listening on http://127.0.0.1:{proxy.Port}  (only teledisk.net is intercepted)");
        Log($"register: {(register.Entries.Count == 0 ? "accept any identity" : register.Entries.Count + " identities")}");
        Log("");
        Log("start the client with:");
        Log($"  set HTTPS_PROXY=http://127.0.0.1:{proxy.Port}");
        Log("");
        Log("waiting for activations, Ctrl+C to stop");

        using var stop = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Set();
        };
        stop.Wait();

        privateKey?.Dispose();
        return 0;
    }

    private static int Launcher(string[] args)
    {
        var appDirectory = Option(args, "--app") ?? throw new UsageException("--app <client directory> is required");
        var outFile = Option(args, "--out") ?? Path.Combine(Home, "teledisk-activated.cmd");
        var port = Option(args, "--port") ?? "8443";
        var executable = Option(args, "--exe") ?? "teledisk.exe";

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);

        var script = $"""
            @echo off
            rem Starts the original client against the local activation server.
            rem Generated by teledisk-license-server.
            setlocal
            set "HTTPS_PROXY=http://127.0.0.1:{port}"
            set "TELEDISK_RES_DIR={Home}"
            start "" "%~dp0..\{executable}"
            endlocal
            """;

        if (File.Exists(Path.Combine(appDirectory, executable)))
        {
            script = script.Replace("%~dp0..\\", appDirectory.TrimEnd('\\') + "\\");
        }

        File.WriteAllText(outFile, script);
        Console.WriteLine($"launcher    : {outFile}");
        Console.WriteLine();
        Console.WriteLine("The launcher sets:");
        Console.WriteLine($"  HTTPS_PROXY       = http://127.0.0.1:{port}   (routes the activation call to this server)");
        Console.WriteLine($"  TELEDISK_RES_DIR  = {Home}   (client reads our public key here)");
        return 0;
    }

    private static int Status()
    {
        Console.WriteLine("teledisk-license-server");
        Console.WriteLine($"  home        : {Home}");
        Console.WriteLine($"  private key : {(File.Exists(PrivateKeyPath) ? "present" : "missing")}");
        Console.WriteLine($"  public key  : {(File.Exists(PublicKeyPath) ? "present" : "missing")}");
        Console.WriteLine($"  certificate : {(File.Exists(CertificatePemPath) ? "present" : "missing")}");
        Console.WriteLine($"  register    : {(File.Exists(RegisterPath) ? "present" : "missing (accept any)")}");

        if (File.Exists(PrivateKeyPath))
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(PrivateKeyPath));
            Console.WriteLine($"  key size    : {rsa.KeySize} bit");
        }

        return 0;
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>Finds the installed client when --app is not given.</summary>
    private static string? DetectAppDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Teledisk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Teledisk"),
        };

        return candidates.FirstOrDefault(directory => File.Exists(Path.Combine(directory, "teledisk.exe")));
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 1;
    }

    private sealed class UsageException(string message) : Exception(message);
}
