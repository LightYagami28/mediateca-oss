namespace Teledisk.Licensing;

/// <summary>
/// Etichette di entitlement, tutte con le stesse funzionalità.
/// </summary>
/// <remarks>
/// Il software è AGPL-3.0-or-later e libero per intero: <see cref="Community"/> è il prodotto
/// completo, senza chiave e senza scadenza. I tier superiori sono solo un'etichetta (comodo per
/// riconoscere chi sostiene il progetto) e non tolgono nulla a chi non ha alcuna licenza.
/// </remarks>
public enum LicenseTier
{
    /// <summary>Nessuna licenza necessaria: prodotto completo. È il default senza file di licenza.</summary>
    Community = 0,

    /// <summary>Etichetta per chi sostiene il progetto.</summary>
    Pro = 1,

    /// <summary>Etichetta di sostegno più alta.</summary>
    Patron = 2,
}
