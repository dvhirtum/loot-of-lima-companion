using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LootOfLima.Server.Games;

/// <inheritdoc cref="IPlayerTokenStore"/>
internal sealed class InMemoryPlayerTokenStore : IPlayerTokenStore
{
    /// <summary>
    /// 32 bytes from a cryptographic source. The token is a bearer credential — holding it is
    /// holding the seat, and the hand that comes with it — so it is sized to be unguessable rather
    /// than merely unique.
    /// </summary>
    private const int TokenBytes = 32;

    private readonly ConcurrentDictionary<string, PlayerIdentity> _byToken = new(StringComparer.Ordinal);

    public PlayerIdentity Issue(string gameId, string displayName, out string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        PlayerIdentity identity = new(gameId, $"p_{Base64Url()}", displayName);
        token = Base64Url();
        _byToken[token] = identity;
        return identity;
    }

    public PlayerIdentity? Resolve(string? token) =>
        token is not null && _byToken.TryGetValue(token, out PlayerIdentity? identity) ? identity : null;

    private static string Base64Url() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
