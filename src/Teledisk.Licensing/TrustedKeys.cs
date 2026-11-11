using System.Security.Cryptography;
using System.Text;

namespace Teledisk.Licensing;

/// <summary>
/// Raccoglie le chiavi pubbliche di cui il client si fida durante la verifica delle licenze.
/// </summary>
/// <remarks>
/// Due sorgenti vengono combinate:
/// <list type="bullet">
///   <item>la chiave incorporata nel progetto, che firma le licenze ufficiali;</item>
///   <item>ogni file <c>*.pub.pem</c> presente in <c>&lt;dati&gt;/keys/trusted</c>, che permette a
///   chiunque di emettere licenze per le proprie build senza toccare la chiave del progetto.</item>
/// </list>
/// </remarks>
public static class TrustedKeys
{
    /// <summary>Cartella, relativa alla directory dati, scansionata per chiavi pubbliche aggiuntive.</summary>
    public const string TrustedFolderName = "keys/trusted";

    /// <summary>Variabile d'ambiente che aggiunge una cartella di chiavi pubbliche fidate.</summary>
    public const string TrustedFolderVariable = "TELEDISK_TRUSTED_KEYS";

    /// <summary>Carica la chiave incorporata nel progetto più ogni chiave fidata locale.</summary>
    public static Dictionary<string, string> Load(string? appDirectory = null)
    {
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (keyId, pem) in EmbeddedProjectKeys())
        {
            keys[keyId] = pem;
        }

        foreach (var folder in TrustedFolders(appDirectory))
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, "*.pem", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var pem = File.ReadAllText(file);
                    keys[ComputeKeyId(pem)] = pem;
                }
                catch (Exception)
                {
                    // Una chiave rotta o illeggibile non deve impedire la verifica delle altre.
                }
            }
        }

        return keys;
    }

    /// <summary>Deriva l'identificatore stabile di una chiave pubblica, senza mai lanciare eccezioni.</summary>
    /// <remarks>
    /// Una chiave malformata non deve impedire la verifica delle altre: se il PEM non è importabile
    /// l'identificatore ripiega su un hash del testo, e il verificatore rifiuta esplicitamente le
    /// licenze che vi fanno riferimento invece di far fallire l'intero archivio di fiducia.
    /// </remarks>
    public static string ComputeKeyId(string? publicKeyPem)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(publicKeyPem))
            {
                return LicenseSigner.ComputeKeyId(publicKeyPem);
            }
        }
        catch (Exception)
        {
            // Prosegue con l'identificatore tollerante derivato dal testo del PEM.
        }

        var normalized = (publicKeyPem ?? "").Replace("\r\n", "\n").Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16].ToLowerInvariant();
    }

    /// <summary>Cartelle scansionate per chiavi pubbliche fidate aggiuntive.</summary>
    public static IEnumerable<string> TrustedFolders(string? appDirectory = null)
    {
        var configured = Environment.GetEnvironmentVariable(TrustedFolderVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return Path.GetFullPath(configured);
        }

        yield return Path.Combine(LicenseAuthority.ResolveDataDirectory(), TrustedFolderName.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>Chiavi pubbliche distribuite con il progetto.</summary>
    public static IEnumerable<(string KeyId, string PublicKeyPem)> EmbeddedProjectKeys()
    {
        foreach (var pem in ProjectKeys.All)
        {
            yield return (ComputeKeyId(pem), pem);
        }
    }
}

/// <summary>
/// Chiavi pubbliche distribuite dentro l'applicazione.
/// </summary>
/// <remarks>
/// Sono pubbliche per definizione, quindi stanno in version control. La chiave privata
/// corrispondente resta in mano ai maintainer fuori dal repository e serve solo a emettere licenze.
/// </remarks>
public static class ProjectKeys
{
    /// <summary>Chiave pubblica dell'issuer ufficiale di Teledisk Community.</summary>
    public const string CommunityIssuer = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAxokB0eag3d9OIuSfn8Oz
        tFA2E8VpvDTw0RipNN4/D29/Jh78Ji7wEz6UAsnF3MBj66sU+nt/KiUufYcfrWLt
        ZudqwSa7bdPoQBUJp/3KPf4mhyXVWyi1cERGE9C4pFoE6+mffKQ8ECDEIezivg8/
        RzE/OyYU9ZIa6G/267bkZjAaZq6zkmdC7VX+xMD92S9kTm7GrBY4+l7y5lJ5VM92
        K7ROu93T4UuRzX1hhe2iws0sZWlp6DILUylRBwMEX9LuF41boFLEF4SkNiYzZJEe
        J6Z9HM9ldyVl1ydAAIHNxHgE5Y2m0kD1joGqgoEg4x1CHYlyWg7r1ZVXEs4Xs0j/
        pQIDAQAB
        -----END PUBLIC KEY-----
        """;

    /// <summary>Ogni chiave del progetto di cui i client si fidano per impostazione predefinita.</summary>
    public static IReadOnlyList<string> All { get; } = [CommunityIssuer];
}
