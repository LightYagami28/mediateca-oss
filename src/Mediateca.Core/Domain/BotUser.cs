namespace Mediateca.Core.Domain;

public enum BotUserRole
{
    User = 0,
    Admin,
    Blocked,
}

public sealed record BotUser
{
    public required long Id { get; init; }

    public string? Username { get; init; }

    public BotUserRole Role { get; init; } = BotUserRole.User;

    public DateTimeOffset FirstSeen { get; init; }

    public DateTimeOffset LastSeen { get; init; }
}
