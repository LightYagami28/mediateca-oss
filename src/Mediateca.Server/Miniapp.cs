namespace Mediateca.Server;

internal static class Miniapp
{
    /// <summary>Foglio di stile servito separatamente per rispettare la CSP senza 'unsafe-inline'.</summary>
    public const string Css = """
        body { margin: 0; font-family: system-ui, sans-serif; background: #0e1116; color: #e8ecf3; }
        header { padding: 14px 18px; font-weight: 600; border-bottom: 1px solid #232c38; }
        main { padding: 16px 18px; }
        .stat { background: #151a21; border: 1px solid #232c38; border-radius: 10px; padding: 12px; margin-bottom: 10px; }
        .label { font-size: 12px; color: #6c7889; }
        .value { font-size: 20px; font-weight: 600; margin-top: 4px; }
        """;

    /// <summary>Script servito separatamente per rispettare la CSP senza 'unsafe-inline'.</summary>
    public const string JavaScript = """
        const token = new URLSearchParams(location.search).get("token") || "";
        fetch("api/stats", { headers: token ? { Authorization: "Bearer " + token } : {} })
          .then(r => r.json())
          .then(d => {
            document.getElementById("stato").textContent = "attivo";
            document.getElementById("elementi").textContent = d.items.toLocaleString("it-IT");
            document.getElementById("spazio").textContent = (d.bytes / 1073741824).toFixed(2) + " GB";
          })
          .catch(() => { document.getElementById("stato").textContent = "non raggiungibile"; });
        """;

    public const string Html = """
        <!DOCTYPE html>
        <html lang="it">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Mediateca</title>
          <link rel="stylesheet" href="/miniapp.css" />
        </head>
        <body>
          <header>Mediateca</header>
          <main>
            <div class="stat"><div class="label">Stato server</div><div class="value" id="stato">…</div></div>
            <div class="stat"><div class="label">Elementi</div><div class="value" id="elementi">…</div></div>
            <div class="stat"><div class="label">Spazio</div><div class="value" id="spazio">…</div></div>
          </main>
          <script src="/miniapp.js"></script>
        </body>
        </html>
        """;
}
