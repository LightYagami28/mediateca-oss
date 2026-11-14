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

    /// <inheritdoc />
    public MediatecaSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new MediatecaSettings { RootDirectory = System.IO.Path.GetDirectoryName(_path) ?? "" };
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<MediatecaSettings>(json, Options)
                   ?? new MediatecaSettings { RootDirectory = System.IO.Path.GetDirectoryName(_path) ?? "" };
        }
        catch (Exception)
        {
            // Un file corrotto non deve impedire l'avvio: si riparte dai valori predefiniti.
            return new MediatecaSettings { RootDirectory = System.IO.Path.GetDirectoryName(_path) ?? "" };
        }
    }

    /// <inheritdoc />
    public void Save(MediatecaSettings settings)
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
}
