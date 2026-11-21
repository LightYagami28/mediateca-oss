using Microsoft.Extensions.Logging;

namespace Mediateca.Mtproto;

/// <summary>Log sorgente-generato: il controllo IsEnabled è nel codice generato.</summary>
internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "File {Name} caricato nel canale {Channel}")]
    internal static partial void FileUploaded(ILogger logger, string name, string channel);

    [LoggerMessage(Level = LogLevel.Information, Message = "File Telegram {Name} salvato in cache")]
    internal static partial void FileCached(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "MTProto connesso come {User}")]
    internal static partial void Connected(ILogger logger, string user);
}
