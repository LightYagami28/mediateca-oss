using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mediateca.Core.Abstractions;

namespace Mediateca.Core.Settings;

/// <summary>
/// Impostazioni su file JSON, con scrittura atomica.
/// </summary>
/// <remarks>
/// Come nell'originale, i dati utente vivono solo su JSON: il codice non scrive mai percorsi o
/// token nella propria configurazione. La scrittura usa un file temporaneo e una sostituzione
/// atomica, così un'interruzione non lascia un JSON troncato.
/// </remarks>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _path;

    /// <summary>Crea lo store sul percorso indicato.</summary>
    public JsonSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = System.IO.Path.GetFullPath(path);
    }

    /// <summary>Percorso del file di impostazioni.</summary>
    public string Path => _path;

    /// <summary>
    /// Legge le impostazioni con semantica best-effort: un file corrotto o non
    /// leggibile viene segnalato con Trace e si riparte dai valori predefiniti.
    /// Per non nascondere l'errore al chiamante usare <see cref="LoadStrict"/>.
    /// </summary>
    /// <inheritdoc />
    public MediatecaSettings Load()
    {
        if (!File.Exists(_path))
        {
            return DefaultSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<MediatecaSettings>(File.ReadAllText(_path), Options)
                   ?? DefaultSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"JsonSettingsStore.Load: uso i valori predefiniti, lettura di '{_path}' fallita: {e.GetType().Name}: {e.Message}");
            return DefaultSettings();
        }
    }

    /// <summary>
    /// Legge le impostazioni e le convalida. L'eccezione sollevata conserva
    /// l'errore di lettura originale come InnerException e riporta tutti i valori
    /// non validi, invece di restituire silenziosamente i predefiniti.
    /// </summary>
    /// <exception cref="InvalidOperationException">Lettura o validazione non riuscita.</exception>
    public MediatecaSettings LoadStrict()
    {
        MediatecaSettings settings;
        try
        {
            settings = File.Exists(_path)
                ? JsonSerializer.Deserialize<MediatecaSettings>(File.ReadAllText(_path), Options) ?? DefaultSettings()
                : DefaultSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"JsonSettingsStore.LoadStrict: lettura di '{_path}' fallita: {e.GetType().Name}: {e.Message}");
            throw new InvalidOperationException($"Impossibile leggere le impostazioni da '{_path}'.", e);
        }

        var problems = settings.Validate();
        if (problems.Count > 0)
        {
            var message = $"Impostazioni non valide in '{_path}': {string.Join("; ", problems)}";
            Trace.WriteLine($"JsonSettingsStore.LoadStrict: {message}");
            throw new InvalidOperationException(message);
        }
        return settings;
    }

    /// <inheritdoc />
    public void Save(MediatecaSettings settings)
    {
        if (settings != null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, Options);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    /// <summary>
    /// Convalida le impostazioni prima di scriverle, evitando di persistere
    /// valori che <see cref="LoadStrict"/> rifiuterebbe al prossimo avvio.
    /// </summary>
    /// <exception cref="InvalidOperationException">Le impostazioni contengono valori non validi.</exception>
    public void SaveValidated(MediatecaSettings settings)
    {
        if (settings != null)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var problems = settings.Validate();
            if (problems.Count > 0)
            {
                var message = $"Impostazioni non valide, nulla è stato scritto: {string.Join("; ", problems)}";
                Trace.WriteLine($"JsonSettingsStore.SaveValidated: {message}");
                throw new InvalidOperationException(message);
            }
            Save(settings);
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private MediatecaSettings DefaultSettings() =>
        new() { RootDirectory = System.IO.Path.GetDirectoryName(_path) ?? "" };
}
