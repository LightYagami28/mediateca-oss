using System.Text;
using Mediateca.Core.Infrastructure;

namespace Mediateca.Cli;

/// <summary>
/// Command line entry point of Mediateca.
/// </summary>
/// <remarks>
/// Deliberately dependency free: the whole surface is plain argument parsing, so the CLI can be
/// published as a single trimmed binary and used on machines without a package manager.
/// </remarks>
public static class Program
{
    private const string Usage = """
        mediateca — Mediateca command line

        USAGE
          mediateca <command> [options]

        COMMANDS
          doctor                      environment report
          db                          create or update the database
          settings [show|set|password] read or change the user settings
          scan <directory>            catalogue a local directory
          backup [--keep N] [--drive] zip the data directory, optionally to Google Drive
          serve [--port N] [--bind A] start the HTTP/WebDAV server

        EXIT CODES
          0 success · 1 usage error
        """;

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args)
    {
        if (args != null)
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
                    "doctor" => Doctor(),
                    "db" => Commands.DatabaseInit(),
                    "settings" => Commands.Settings(args[1..]),
                    "scan" => Commands.Scan(args[1..]),
                    "backup" => Commands.Backup(args[1..]).GetAwaiter().GetResult(),
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
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static int Doctor()
    {
        Console.WriteLine("mediateca doctor");
        Console.WriteLine($"  os            : {Environment.OSVersion}");
        Console.WriteLine($"  runtime       : {Environment.Version}");
        Console.WriteLine($"  data dir      : {MediatecaPaths.Default().Root}");
        Console.WriteLine($"  entitlement   : all features, no license required");

        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        return 1;
    }

    /// <summary>Raised when the command line is malformed.</summary>
    public sealed class UsageException(string message) : Exception(message);
}
