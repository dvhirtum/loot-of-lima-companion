using LootOfLima.Contracts;
using LootOfLima.Rules;
using LootOfLima.Server.Games;
using LootOfLima.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Connected = LootOfLima.Server.Hubs.Connection;
using Registry = LootOfLima.Server.Hubs.InMemoryConnectionRegistry;
using Stored = LootOfLima.Server.Games.StoredGame;
using Sut = LootOfLima.Server.Hubs.GameBroadcaster;

namespace LootOfLima.Server.Tests.Hubs.GameBroadcaster;

/// <summary>
/// The fan-out itself, driven against a stub hub so a send can be made to fail on purpose.
/// </summary>
/// <remarks>
/// The integration tests cover what a real client receives, which is the property that matters, but
/// they cannot arrange for one connection out of three to break: a client that has actually gone is
/// detached before the loop ever reaches it. Standing in for the transport is the only way to hold
/// a connection still and have it throw.
/// </remarks>
public class BroadcastTests
{
    [Fact]
    public async Task Should_still_reach_the_others_when_one_connection_fails()
    {
        // Arrange
        // A send fails when a browser has closed and the server has not noticed yet. That is one
        // player's problem, and it must not leave everybody after them in the list on a stale board.
        RecordingHub hub = new() { FailFor = "b" };
        Registry connections = new();
        Stored game = Seated();

        foreach (string id in (string[])["a", "b", "c"])
        {
            connections.Attach(new Connected(id, game.State.GameId, Player: null));
        }

        Sut broadcaster = new(hub, connections, NullLogger<Sut>.Instance);

        // Act
        await broadcaster.BroadcastAsync(game);

        // Assert
        Assert.Equal(["a", "c"], hub.Delivered.Select(sent => sent.ConnectionId).Order());
    }

    [Fact]
    public async Task Should_stamp_every_frame_with_the_version_when_the_game_is_sent()
    {
        // Arrange
        // The stamp is what lets a client tell a frame that arrived late from one that is current.
        RecordingHub hub = new();
        Registry connections = new();
        Stored game = Seated() with { Version = 7 };

        connections.Attach(new Connected("a", game.State.GameId, Player: null));
        connections.Attach(new Connected("b", game.State.GameId, Player: null));

        Sut broadcaster = new(hub, connections, NullLogger<Sut>.Instance);

        // Act
        await broadcaster.BroadcastAsync(game);

        // Assert
        Assert.All(hub.Delivered, sent => Assert.Equal(7, ((PlayerView)sent.Payload).Version));
    }

    [Fact]
    public async Task Should_send_to_nobody_else_when_one_connection_is_caught_up()
    {
        // Arrange
        // What Rejoin does. An arrival changes no seat, no hand and no history, so there is nothing
        // in it for the rest of the table to be told.
        RecordingHub hub = new();
        Registry connections = new();
        Stored game = Seated();

        connections.Attach(new Connected("a", game.State.GameId, Player: null));
        connections.Attach(new Connected("b", game.State.GameId, Player: null));

        Sut broadcaster = new(hub, connections, NullLogger<Sut>.Instance);

        // Act
        await broadcaster.SendCurrentAsync(game, "b");

        // Assert
        Assert.Equal("b", Assert.Single(hub.Delivered).ConnectionId);
    }

    private static Stored Seated() => new(TestGames.Lobby(), Version: 1);
}

/// <summary>A hub context that keeps what it was asked to send, and can refuse one connection.</summary>
internal sealed class RecordingHub : IHubContext<LootOfLima.Server.Hubs.GameHub>
{
    private readonly HubClients _clients;

    public RecordingHub() => _clients = new HubClients(this);

    public string? FailFor { get; init; }

    public List<(string ConnectionId, string Method, object Payload)> Delivered { get; } = [];

    public IHubClients Clients => _clients;

    public IGroupManager Groups => throw new NotSupportedException("This server never uses groups.");

    private sealed class HubClients(RecordingHub owner) : IHubClients
    {
        public IClientProxy All => throw new NotSupportedException("This server never fans out.");

        public IClientProxy AllExcept(IReadOnlyList<string> excluded) =>
            throw new NotSupportedException("This server never fans out.");

        public IClientProxy Client(string connectionId) => new Proxy(owner, connectionId);

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) =>
            throw new NotSupportedException("This server never fans out.");

        public IClientProxy Group(string groupName) =>
            throw new NotSupportedException("This server never uses groups.");

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excluded) =>
            throw new NotSupportedException("This server never uses groups.");

        public IClientProxy Groups(IReadOnlyList<string> groupNames) =>
            throw new NotSupportedException("This server never uses groups.");

        public IClientProxy User(string userId) =>
            throw new NotSupportedException("This server has no user identities.");

        public IClientProxy Users(IReadOnlyList<string> userIds) =>
            throw new NotSupportedException("This server has no user identities.");
    }

    private sealed class Proxy(RecordingHub owner, string connectionId) : IClientProxy
    {
        public Task SendCoreAsync(
            string method, object?[] arguments, CancellationToken cancellationToken = default)
        {
            if (string.Equals(connectionId, owner.FailFor, StringComparison.Ordinal))
            {
                throw new IOException($"Connection {connectionId} has gone.");
            }

            owner.Delivered.Add((connectionId, method, arguments[0]!));
            return Task.CompletedTask;
        }
    }
}
