namespace Mediateca.Core.Domain;

public enum AddressBookKind
{
    User = 0,
    Bot,
    Group,
    Channel,
}

public sealed record AddressBookEntry
{
    public required long PeerId { get; init; }

    public long? AccessHash { get; init; }

    public AddressBookKind Kind { get; init; } = AddressBookKind.User;

    public required string Title { get; init; }

    public string? Username { get; init; }

    public string? Phone { get; init; }

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
