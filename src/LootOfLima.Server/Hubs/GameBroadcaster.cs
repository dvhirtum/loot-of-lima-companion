using LootOfLima.Contracts;
using LootOfLima.Projection;
using LootOfLima.Rules;
using LootOfLima.Server.Games;
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
/// <para>
/// Nothing here takes a cancellation token. A broadcast is owed to the table rather than to
/// whoever caused it, and the obvious token to hand it — the acting connection's
/// <c>ConnectionAborted</c> — belongs to one player: a phone that locks its screen as its owner's
/// question commits would have cancelled everybody else's frame. Delivery to one connection is
/// likewise isolated from delivery to the next, so a connection that has gone away takes its own
/// frame down and nobody else's.
/// </para>
/// </remarks>
internal sealed class GameBroadcaster(
    IHubContext<GameHub> hub,
    IConnectionRegistry connections,
    ILogger<GameBroadcaster> logger)
{
    internal const string StateMethod = "State";

    internal const string EventMethod = "Event";

    /// <summary>Sends the game to everybody watching it, each seeing only their own view.</summary>
    public Task BroadcastAsync(StoredGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return SendToEachAsync(game, happened: null);
    }

    /// <summary>
    /// Sends the game and a nudge saying what changed. The state still goes per connection; the
    /// event carries no game detail, so the same one is safe for everybody.
    /// </summary>
    public Task BroadcastAsync(StoredGame game, GameEventDto happened)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(happened);

        return SendToEachAsync(game, happened);
    }

    /// <summary>
    /// One walk of the connections, sending each its own state and then the nudge.
    /// </summary>
    /// <remarks>
    /// One walk rather than two, because the list can change between them: a connection attaching
    /// in the gap would be told what happened without having been sent the state it happened to,
    /// which is the one order these two must never arrive in.
    /// </remarks>
    private async Task SendToEachAsync(StoredGame game, GameEventDto? happened)
    {
        foreach (Connection connection in connections.InGame(game.State.GameId))
        {
            await SendAsync(game, connection).ConfigureAwait(false);

            if (happened is not null)
            {
                await DeliverAsync(connection, EventMethod, happened).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sends the game to one connection, for a client that has just arrived and has nothing yet.
    /// </summary>
    /// <remarks>
    /// Somebody connecting changes no seat, no hand and no history, so there is nothing in it for
    /// anybody else to be told. Pushing the arrival to the whole table would also mean pushing a
    /// state that was read outside the store's gate, which a reconnect landing mid-command could
    /// use to rewrite every client's view back to before that command.
    /// </remarks>
    public async Task SendCurrentAsync(StoredGame game, string connectionId)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (connections.Find(connectionId) is { } connection)
        {
            await SendAsync(game, connection).ConfigureAwait(false);
        }
    }

    private Task SendAsync(StoredGame game, Connection connection)
    {
        PlayerView view = Projector.Project(game.State, ViewerSeat(game.State, connection.Player?.PlayerId));
        return DeliverAsync(connection, StateMethod, view with { Version = game.Version });
    }

    /// <summary>
    /// One send, and one connection's problems kept to itself.
    /// </summary>
    /// <remarks>
    /// A send fails when the connection has gone — a closed tab, a phone off the network — which
    /// is ordinary and is not the table's business. Letting it out would abandon the rest of the
    /// loop and leave the players after it in the list looking at a stale board because somebody
    /// else's browser closed.
    /// </remarks>
    private async Task DeliverAsync(Connection connection, string method, object payload)
    {
        try
        {
            await hub.Clients.Client(connection.ConnectionId)
                .SendAsync(method, payload, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception, "Could not send {Method} to connection {ConnectionId}.",
                method, connection.ConnectionId);
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
