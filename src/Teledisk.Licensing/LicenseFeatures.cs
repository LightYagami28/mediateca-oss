namespace Teledisk.Licensing;

/// <summary>
/// Optional capabilities that a license can unlock on top of the always-available core.
/// </summary>
[Flags]
public enum LicenseFeatures
{
    /// <summary>No optional capability.</summary>
    None = 0,

    /// <summary>Local media library, HTTP server and read-only WebDAV. Always available in Community.</summary>
    CoreLibrary = 1 << 0,

    /// <summary>Telegram bot integration.</summary>
    TelegramBot = 1 << 1,

    /// <summary>Remote access to the HTTP/WebDAV server from outside the local machine.</summary>
    RemoteAccess = 1 << 2,

    /// <summary>Cloud backup targets (for example a drive provider).</summary>
    CloudBackup = 1 << 3,

    /// <summary>More than one bot identity active at the same time.</summary>
    MultiBot = 1 << 4,

    /// <summary>Third-party metadata providers (film, series and music catalogues).</summary>
    MetadataProviders = 1 << 5,

    /// <summary>Scheduled and differential backups.</summary>
    ScheduledBackup = 1 << 6,

    /// <summary>Every optional capability.</summary>
    All = CoreLibrary | TelegramBot | RemoteAccess | CloudBackup | MultiBot | MetadataProviders | ScheduledBackup,
}
