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
        form { margin-top: 14px; display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
        input, button { background: #0e1116; color: #e8ecf3; border: 1px solid #232c38; border-radius: 6px; padding: 8px; }
        #errore { color: #ff8a8a; }
        """;

    /// <summary>Script servito separatamente per rispettare la CSP senza 'unsafe-inline'.</summary>
    /// <remarks>Il token viaggia solo nel corpo del POST iniziale: mai nella query string, dove
    /// finirebbe nei log, nella cronologia e nel Referer.</remarks>
    public const string JavaScript = """
        const form = document.getElementById("accesso");
        const campo = document.getElementById("token");
        const errore = document.getElementById("errore");

        async function carica() {
          try {
            const risposta = await fetch("api/stats", { credentials: "same-origin" });
            if (risposta.status === 401) {
              form.hidden = false;
              return;
            }
            if (!risposta.ok) throw new Error("risposta non valida");
            const dati = await risposta.json();
            document.getElementById("stato").textContent = "attivo";
            document.getElementById("elementi").textContent = dati.items.toLocaleString("it-IT");
            document.getElementById("spazio").textContent = (dati.bytes / 1073741824).toFixed(2) + " GB";
            form.hidden = true;
          } catch {
            document.getElementById("stato").textContent = "non raggiungibile";
          }
        }

        form.addEventListener("submit", async (evento) => {
          evento.preventDefault();
          errore.textContent = "";
          try {
            const risposta = await fetch("api/session", {
              method: "POST",
              credentials: "same-origin",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ token: campo.value }),
            });
            campo.value = "";
            if (!risposta.ok) {
              errore.textContent = "token non valido";
              return;
            }
            await carica();
          } catch {
            errore.textContent = "invio non riuscito";
          }
        });

        carica();
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
            <form id="accesso" hidden>
              <label for="token">Token API</label>
              <input id="token" type="password" autocomplete="off" />
              <button type="submit">Accedi</button>
              <span id="errore" role="alert"></span>
            </form>
          </main>
          <script src="/miniapp.js"></script>
        </body>
        </html>
        """;
}
