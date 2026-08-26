using LootOfLima.Contracts;
using LootOfLima.Projection;
using LootOfLima.Rules;
using Microsoft.AspNetCore.SignalR;

namespace LootOfLima.Server.Hubs;

/// <summary>
/// The one place game state is sent to anybody.
/// </summary>
/// <remarks>
/// <para>
/// Every push walks the game's connections and sends <see cref="Projector.Project"/> per
/// connection, for that connection's viewer seat. <c>Clients.All</c>, <c>Clients.Group</c> and
/// <c>Clients.Others</c> are never used for state anywhere in this server, because they send one
/// set of bytes to several viewers and no single set of bytes is correct for two different seats.
/// </para>
/// <para>
/// Having exactly one method that can send state is the point: a leak has one place to hide, and a
/// test can read this file and check it.
/// </para>
/// </remarks>
internal sealed class GameBroadcaster(IHubContext<GameHub> hub, IConnectionRegistry connections)
{
    internal const string StateMethod = "State";

    internal const string EventMethod = "Event";

    /// <summary>Sends the game to everybody watching it, each seeing only their own view.</summary>
    public async Task BroadcastAsync(GameState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (Connection connection in connections.InGame(state.GameId))
        {
            PlayerView view = Projector.Project(state, ViewerSeat(state, connection.Player?.PlayerId));
            await hub.Clients.Client(connection.ConnectionId)
                .SendAsync(StateMethod, view, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends the game and a nudge saying what changed. The state still goes per connection; the
    /// event carries no game detail, so the same one is safe for everybody.
    /// </summary>
    public async Task BroadcastAsync(
        GameState state,
        GameEventDto happened,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await BroadcastAsync(state, cancellationToken).ConfigureAwait(false);

        foreach (Connection connection in connections.InGame(state.GameId))
        {
            await hub.Clients.Client(connection.ConnectionId)
                .SendAsync(EventMethod, happened, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The seat a connection is entitled to see, found by identity rather than by anything the
    /// client said. A player with no seat yet, and the host's screen, both project as a spectator.
    /// </summary>
    private static int? ViewerSeat(GameState state, string? playerId) =>
        playerId is null
            ? null
            : state.Seats.FirstOrDefault(
                seat => string.Equals(seat.PlayerId, playerId, StringComparison.Ordinal))?.Index;
}
