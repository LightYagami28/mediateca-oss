using Microsoft.AspNetCore.Http;

namespace Mediateca.Server;

/// <summary>
/// Header di sicurezza applicati a ogni risposta, incluse le risposte di errore.
/// La CSP ammette solo sorgenti <c>'self'</c> e include
/// <c>upgrade-insecure-requests</c>, come richiesto per OWASP A02 (2025).
/// </summary>
/// <remarks>
/// HSTS ha effetto solo se il server è esposto tramite TLS 1.3: in produzione il
/// bind loopback predefinito va sostituito da un termine TLS davanti al servizio.
/// </remarks>
internal static class SecurityHeaders
{
    /// <summary>Policy CSP con sole sorgenti della stessa origine e upgrade delle richieste.</summary>
    internal const string Csp =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; " +
        "upgrade-insecure-requests";

    /// <summary>Applica gli header e passa al middleware successivo.</summary>
    internal static async Task Apply(HttpContext context, RequestDelegate next)
    {
        var headers = context.Response.Headers;
        headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains; preload";
        headers.UpgradeInsecureRequests = "1";
        headers.ContentSecurityPolicy = Csp;
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/dav"))
        {
            headers.CacheControl = "no-store";
        }

        await next(context).ConfigureAwait(false);
    }
}
