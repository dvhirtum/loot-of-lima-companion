using System.Text.Json;
using LootOfLima.Rules;
using LootOfLima.Server.Games;
using LootOfLima.Server.Hubs;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LootOfLima.Server.Tests.Serialization.HiddenStateGuard;

/// <summary>
/// The guard doing the job it exists for, against the real hub, with a real client.
/// </summary>
/// <remarks>
/// <para>
/// The other two suites check the halves: <c>GetTypeInfoTests</c> that the guard refuses what it
/// should, and <c>WiringTests</c> that the built application actually consults it. Neither shows
/// what a client receives when the thing they guard against happens, and that is the only question
/// the guard was added to answer.
/// </para>
/// <para>
/// So this arranges the leak. It reaches past the hub, past the broadcaster and past the
/// projection, takes the hub context the framework itself uses, and pushes a <see cref="Seat"/> —
/// somebody's dealt hand — straight at a connected client. Nothing in the type system can stop
/// that, which is exactly the case the guard is the last line against.
/// </para>
/// <para>
/// Note what is deliberately <em>not</em> asserted: that the bytes carry no location name. A raw
/// <see cref="Seat"/> writes <see cref="LocationId"/> as <c>{"terrain":0,"number":3}</c> rather
/// than as <c>B1</c>, so the substring search the leak suite relies on would not have caught this
/// payload at all. That suite is right about the frames it reads, which are projections and do
/// name their locations; it is not a net under raw engine types. This is.
/// </para>
/// </remarks>
public class LastResortTests
{
    private const string GameId = "guarded";

    private const string RoomCode = "GRDD";

    [Fact]
    public async Task Should_send_nothing_and_drop_the_connection_when_a_hand_is_pushed_at_a_client()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        GameState game = await SeedAsync(app);
        Assert.NotEmpty(game.SeatAt(0).Hand);

        // Both are written on the SignalR dispatch thread and read from this one, which is what
        // Received exists for. A bare list here would be the very defect this file is about to
        // demonstrate, in the file demonstrating it.
        Received<Exception?> closed = new();
        Received<string> frames = new();

        await using HubConnection connection = Connect(app);
        connection.On<JsonElement>("State", frame => frames.Add(frame.GetRawText()));
        connection.Closed += error =>
        {
            closed.Add(error);
            return Task.CompletedTask;
        };

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connection.InvokeAsync("Rejoin", RoomCode, null, TestContext.Current.CancellationToken);

        // Rejoin returning means the hub method finished, not that the frame it sent has reached
        // the handler. Taking the baseline before it lands would set it one too low and then read
        // that arrival as though it were the thing under test.
        await WaitUntilAsync(() => frames.Count == 1);
        int legitimate = frames.Count;

        // Act
        // Straight at the client, bypassing every layer that would otherwise have prevented it.
        await app.Services.GetRequiredService<IHubContext<GameHub>>()
            .Clients.Client(ConnectionId(app))
            .SendAsync("State", game.SeatAt(0), TestContext.Current.CancellationToken);

        // Either outcome settles it: the connection reports itself closed, or something arrived
        // that should not have. Waiting only for the first would report a leak as a timeout.
        //
        // Waited on `closed` and not on `connection.State`, which is the weaker of the two and was
        // a race. The state flips to Disconnected before the `Closed` handler has run, so a wait on
        // the state could return with `closed` still empty and the last assertion below reading
        // "the collection was empty" -- a real failure message about a guard that had in fact
        // worked. It is load-dependent, so it surfaced as one test in a suite occasionally going
        // red and then passing alone, which is the least useful shape a failure has.
        await WaitUntilAsync(() => closed.Count > 0 || frames.Count > legitimate);

        // Assert
        // The hand did not reach the client. The message carries whatever did, because a leak is
        // worth reading in full the one time it happens.
        IReadOnlyList<string> arrived = frames.Snapshot();
        Assert.True(
            arrived.Count == legitimate,
            "A hand reached a client: " + string.Join(" | ", arrived.Skip(legitimate)));

        // And the connection went down rather than quietly carrying on, which is what makes this a
        // failure somebody notices rather than a frame that silently never arrived.
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
        Assert.Equal("HubException", Assert.Single(closed.Snapshot())?.GetType().Name);
    }

    [Fact]
    public async Task Should_keep_serving_the_table_when_the_view_is_the_one_being_sent()
    {
        // Arrange
        // The other half, and the one that would break if the guard were ever made too eager: the
        // shape a client is entitled to has to go through untouched.
        await using WebApplicationFactory<Program> app = new();
        await SeedAsync(app);

        Received<string> frames = new();

        await using HubConnection connection = Connect(app);
        connection.On<JsonElement>("State", frame => frames.Add(frame.GetRawText()));

        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connection.InvokeAsync("Rejoin", RoomCode, null, TestContext.Current.CancellationToken);

        await WaitUntilAsync(() => frames.Count == 1);
        int legitimate = frames.Count;

        // Act
        await app.Services.GetRequiredService<GameBroadcaster>()
            .BroadcastAsync(new StoredGame(await CurrentAsync(app), Version: 99));

        await WaitUntilAsync(() => frames.Count > legitimate);

        // Assert
        Assert.Equal(legitimate + 1, frames.Count);
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    /// <summary>A dealt game, so the seat pushed at a client is holding a real hand.</summary>
    private static async Task<GameState> SeedAsync(WebApplicationFactory<Program> app)
    {
        IReadOnlyList<SeatSpec> seats =
        [
            .. Enumerable.Range(0, 5).Select(index => new SeatSpec(SeatKind.Bot, $"Bot {index + 1}")),
        ];

        Result<GameState> created = GameEngine.CreateGame(GameId, RoomCode, seats, TestGames.DefaultSeed);
        Assert.Null(created.Error);

        // A game needs somebody at the table before it can be dealt, and a dealt game is the point:
        // an empty hand would prove nothing about whether a hand can escape.
        Result<GameState> claimed = GameEngine.ClaimSeat(created.Value, 0, "p_seated", "Ada");
        Assert.Null(claimed.Error);

        Result<GameState> started = GameEngine.Start(
            claimed.Value,
            Dealer.Create(5, TestGames.DefaultSeed),
            new DeterministicShuffleSource(TestGames.DefaultSeed));

        Assert.Null(started.Error);

        Assert.True(await app.Services.GetRequiredService<IGameStore>()
            .TryAddAsync(started.Value, TestContext.Current.CancellationToken));

        return started.Value;
    }

    private static async Task<GameState> CurrentAsync(WebApplicationFactory<Program> app) =>
        (await app.Services.GetRequiredService<IGameStore>()
            .FindAsync(GameId, TestContext.Current.CancellationToken))!.State;

    private static string ConnectionId(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<IConnectionRegistry>().InGame(GameId)[0].ConnectionId;

    private static HubConnection Connect(WebApplicationFactory<Program> app) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(app.Server.BaseAddress, "hub/game"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

    /// <summary>
    /// Waits for something the server does on its own, rather than in answer to a call.
    /// </summary>
    /// <remarks>
    /// A serialization failure happens on the connection's write pump, after the send that caused
    /// it has already returned, so there is nothing to await for it — only a state to watch for.
    /// </remarks>
    private static async Task WaitUntilAsync(Func<bool> settled)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!settled())
        {
            Assert.True(DateTime.UtcNow < deadline, "The server never got there.");
            await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken);
        }
    }
}
