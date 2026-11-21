// Copyright (c) 2026 Mediateca contributors
// Licensed under AGPL-3.0-or-later. See LICENSE in the project root.

namespace Mediateca.Backup;

/// <summary>
/// Esito di un backup completato con successo.
/// </summary>
public sealed record BackupResult
{
    /// <summary>Percorso completo dell'archivio creato.</summary>
    public required string ArchivePath { get; init; }

    /// <summary>Dimensione dell'archivio in byte.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Digest SHA-256 dell'archivio, esadecimale minuscolo.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Tempo impiegato a creare l'archivio.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Momento di creazione, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Numero di archivi rimossi dalla retention.</summary>
    public int RemovedOldArchives { get; init; }

    public string? RemoteId { get; init; }

    /// <summary>Nome del file dell'archivio.</summary>
    public string FileName => Path.GetFileName(ArchivePath);
}

/// <summary>
/// Destinazione di backup riutilizzabile dalla CLI e dal server.
/// </summary>
public interface IBackupTarget
{
    /// <summary>Nome breve del tipo di destinazione, per esempio <c>locale</c>.</summary>
    string Name { get; }

    /// <summary>Crea un backup completo di <paramref name="sourceDirectory"/> in <paramref name="destinationDirectory"/>.</summary>
    /// <param name="sourceDirectory">Cartella da salvare, letta ricorsivamente.</param>
    /// <param name="destinationDirectory">Cartella in cui scrivere l'archivio; viene creata se assente.</param>
    /// <param name="cancellationToken">Annullamento dell'operazione.</param>
    Task<BackupResult> CreateAsync(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default);
}
