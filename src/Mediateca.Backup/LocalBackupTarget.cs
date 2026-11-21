using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Mediateca.Backup;

/// <summary>
/// Backup locale in archivio ZIP datato, con retention dei più recenti.
/// </summary>
/// <remarks>
/// L'archivio è nominato con la data e l'ora di creazione e ne viene calcolato lo SHA-256 per la
/// verifica successiva. La sottocartella dei backup (per convenzione <c>backups</c> dentro la
/// sorgente) e la stessa destinazione, quando vive sotto la sorgente, sono escluse dalla copia,
/// così un backup non contiene mai se stesso né quelli precedenti.
/// </remarks>
public sealed class LocalBackupTarget : IBackupTarget
{
    /// <summary>Nome della sottocartella dei backup esclusa dalla copia.</summary>
    public const string BackupsFolderName = "backups";

    /// <summary>Prefisso predefinito dei nomi archivio.</summary>
    public const string DefaultArchivePrefix = "mediateca-backup";

    /// <summary>Crea la destinazione con la retention predefinita.</summary>
    public LocalBackupTarget()
    {
    }

    /// <summary>Crea la destinazione con una retention esplicita.</summary>
    /// <param name="maxBackups">Numero di archivi da conservare; non positivo significa nessun limite.</param>
    public LocalBackupTarget(int maxBackups) => MaxBackups = maxBackups;

    /// <inheritdoc />
    public string Name => "locale";

    /// <summary>Numero di archivi da conservare dopo ogni backup; non positivo significa nessun limite.</summary>
    public int MaxBackups { get; init; } = 14;

    /// <summary>Prefisso dei nomi archivio.</summary>
    public string ArchivePrefix { get; init; } = DefaultArchivePrefix;

    /// <inheritdoc />
    /// <exception cref="DirectoryNotFoundException">Se la cartella sorgente non esiste.</exception>
    public Task<BackupResult> CreateAsync(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        if (sourceDirectory != null)
        {
            if (destinationDirectory != null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
                ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

                var source = Path.GetFullPath(sourceDirectory);
                var destination = Path.GetFullPath(destinationDirectory);
                if (!Directory.Exists(source))
                {
                    throw new DirectoryNotFoundException($"Cartella sorgente inesistente: {source}");
                }

                Directory.CreateDirectory(destination);
                return Task.Run(() => CreateArchive(source, destination, cancellationToken), cancellationToken);
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private BackupResult CreateArchive(string source, string destination, CancellationToken cancellationToken)
    {
        if (destination != null)
        {
            if (source != null)
            {
                var stopwatch = Stopwatch.StartNew();
                var archivePath = NextArchivePath(destination);
                var backupFolder = Path.Combine(source, BackupsFolderName);
                var sameRoot = PathsEqual(source, destination);

                // I file sono elencati prima di creare l'archivio, così il nuovo zip non entra nella copia.
                var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                    .Where(file => !ShouldSkip(file, backupFolder, destination, sameRoot, archivePath))
                    .ToList();

                try
                {
                    using (var stream = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                    {
                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var entryName = Path.GetRelativePath(source, file).Replace('\\', '/');
                            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                        }
                    }
                }
                catch
                {
                    _ = TryDelete(archivePath);
                    throw;
                }

                long size;
                string digest;
                using (var stream = File.OpenRead(archivePath))
                {
                    size = stream.Length;
                    digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                }

                var removed = ApplyRetention(destination);
                stopwatch.Stop();

                return new BackupResult
                {
                    ArchivePath = archivePath,
                    SizeBytes = size,
                    Sha256 = digest,
                    Duration = stopwatch.Elapsed,
                    RemovedOldArchives = removed,
                };
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static bool ShouldSkip(string file, string backupFolder, string destination, bool sameRoot, string archivePath)
    {
        if (file != null)
        {
            if (archivePath != null)
            {
                if (IsUnder(file, backupFolder))
                {
                    return true;
                }

                if (!sameRoot && IsUnder(file, destination))
                {
                    return true;
                }

                return string.Equals(file, archivePath, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private string NextArchivePath(string destination)
    {
        if (destination != null)
        {
            var prefix = string.IsNullOrWhiteSpace(ArchivePrefix) ? DefaultArchivePrefix : ArchivePrefix;
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var candidate = Path.Combine(destination, $"{prefix}-{stamp}.zip");

            var counter = 1;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(destination, $"{prefix}-{stamp}-{counter++}.zip");
            }

            return candidate;
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private int ApplyRetention(string destination)
    {
        if (destination != null)
        {
            if (MaxBackups < 1)
            {
                return 0;
            }

            var prefix = string.IsNullOrWhiteSpace(ArchivePrefix) ? DefaultArchivePrefix : ArchivePrefix;
            var stale = Directory.EnumerateFiles(destination, $"{prefix}-*.zip")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Skip(MaxBackups)
                .ToList();

            return stale.Count(path => TryDelete(path));
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static bool TryDelete(string path)
    {
        if (path != null)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static bool IsUnder(string file, string directory)
    {
        if (directory != null)
        {
            if (file != null)
            {
                var relative = Path.GetRelativePath(directory, file);
                return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
