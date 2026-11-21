using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Mediateca.Server;

/// <summary>
/// Certificato autofirmato usato dal server locale quando non e' configurato un termine TLS.
/// </summary>
/// <remarks>
/// Il certificato viene generato in memoria a ogni avvio: non c'e' una chiave da distribuire
/// ne' un file da proteggere. I client che parlano con il server locale devono accettare
/// questo certificato autofirmato, esattamente come accadrebbe con un certificato di sviluppo.
/// </remarks>
internal static class TlsCertificate
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    internal static X509Certificate2 Create(string bindAddress)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(ServerAuthenticationOid)], false));

        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        if (IPAddress.TryParse(bindAddress, out var address)
            && !address.Equals(IPAddress.Any)
            && !address.Equals(IPAddress.IPv6Any))
        {
            names.AddIpAddress(address);
        }

        request.CertificateExtensions.Add(names.Build());

        using var signed = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        // Il certificato autofirmato deve possedere la propria chiave: importandolo dal PFX
        // non dipende dalla durata dell'oggetto RSA usato per generarlo.
        return X509CertificateLoader.LoadPkcs12(signed.Export(X509ContentType.Pfx), password: null);
    }
}
