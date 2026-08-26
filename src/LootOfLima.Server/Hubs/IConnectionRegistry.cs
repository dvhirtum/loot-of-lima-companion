using LootOfLima.Server.Games;

namespace LootOfLima.Server.Hubs;

/// <summary>One live SignalR connection and who is on the other end of it.</summary>
/// <param name="Player">
/// Null for a spectator — the host's screen, or somebody watching. A spectator is entitled to
/// strictly less than any player, which is exactly what a null viewer seat projects to. The whole
/// identity is captured here at rejoin time so no later step has to go looking for it.
/// </param>
internal sealed record Connection(string ConnectionId, string GameId, PlayerIdentity? Player);

/// <summary>
/// Which connections are watching which game.
/// </summary>
/// <remarks>
/// This exists because state is pushed per connection rather than per group: the broadcaster walks
/// the connections itself and projects the game separately for each viewer. A SignalR group cannot
/// do that — every member of a group receives the same bytes, which is the one thing this
/// application must never send.
/// </remarks>
internal interface IConnectionRegistry
{
    /// <summary>
    /// Binds a connection to a game and an identity. Idempotent per connection: rejoining twice
    /// replaces the binding rather than adding a second one.
    /// </summary>
    void Attach(Connection connection);

    void Detach(string connectionId);

    /// <summary>Every connection currently watching a game, in no particular order.</summary>
    IReadOnlyList<Connection> InGame(string gameId);

    Connection? Find(string connectionId);
}
