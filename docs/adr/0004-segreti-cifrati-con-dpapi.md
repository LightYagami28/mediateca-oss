# 0004 - Segreti cifrati con la protezione dati dell'utente

Stato: accettata (2026-11-14)

## Contesto

Token del bot, ApiHash di Telegram e token API devono stare nelle impostazioni dell'utente.

## Decisione

Cifrarli con DPAPI (`System.Security.Cryptography.ProtectedData`) prima di scrivere
`settings.json`; la chiave TMDB si legge da `MEDIATECA_TMDB_API_KEY` o dalle impostazioni.
Scrittura atomica del file di impostazioni.

## Conseguenze

I segreti valgono solo per lo stesso utente Windows sulla stessa macchina; su
un'altra vanno
reimpostati. Nessun segreto nel repository.
