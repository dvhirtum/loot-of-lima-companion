using LootOfLima.Rules;
using LootOfLima.Server.Games;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LootOfLima.Server.Tests.Hubs.GameHub;

/// <summary>
/// What a client arriving alongside a command ends up looking at.
/// </summary>
/// <remarks>
/// <para>
/// A connection is not sent broadcasts until it is attached, and the state it is sent on arrival is
/// whatever was read at the time. Read before attaching, and a command committing in between is
/// missed twice over: the broadcast went to a list this connection was not on, and the snapshot it
/// then receives predates the command. Nothing re-sends, so the client sits there stale — and if
/// the command was <c>StartGame</c>, it sits on a lobby with nothing to click while the table waits
/// for it.
/// </para>
/// <para>
/// The window is real but narrow, so it is widened here rather than raced for. The store is
/// wrapped, and the wrapper applies a command at the exact moment the hub looks the game up. That
/// makes the interleaving certain instead of occasional, which is the difference between a test
/// that guards this and a test that usually agrees with it.
/// </para>
/// </remarks>
public class RejoinOrderingTests
{
    private const string GameId = "interleaved";

    private const string RoomCode = "INTL";

    [Fact]
    public async Task Should_send_the_state_after_the_command_when_one_commits_as_a_client_arrives()
    {
        // Arrange
        Interleaving store = new(new InMemoryGameStore());

        await using WebApplicationFactory<Program> app = Wrap(store);
        await SeedAsync(app, store);

        // The command lands while the hub is looking the room code up: after the read that finds
        // the game, and before this connection is on the list a broadcast would reach.
        store.DuringRoomCodeLookup = () => store.MutateAsync(
            GameId, state => GameEngine.ClaimSeat(state, 0, "p_other", "Grace"));

        List<long> versions = [];
        await using HubConnection connection = Connect(app);
        connection.On<Contracts.PlayerView>("State", view => versions.Add(view.Version));

        // Act
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await connection.InvokeAsync("Rejoin", RoomCode, null, TestContext.Current.CancellationToken);

        await WaitUntilAsync(() => versions.Count > 0);

        // Assert
        // The arriving client is not behind the store. It missed the broadcast, so this is the only
        // frame it will get until somebody acts again, and it has to be the current one.
        StoredGame current = (await store.FindAsync(GameId, TestContext.Current.CancellationToken))!;
        Assert.Equal(current.Version, versions[^1]);
        Assert.True(current.State.SeatAt(0).IsHuman, "The interleaved command did not actually run.");
    }

    private static WebApplicationFactory<Program> Wrap(IGameStore store) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(
            builder => builder.ConfigureServices(
                services =>
                {
                    services.RemoveAll<IGameStore>();
                    services.AddSingleton(store);
                }));

    private static async Task SeedAsync(WebApplicationFactory<Program> app, IGameStore store)
    {
        // Touched so the factory builds its host before the store is used from outside it.
        using HttpClient started = app.CreateClient();

        IReadOnlyList<SeatSpec> seats =
        [
            .. Enumerable.Range(0, 5).Select(index => new SeatSpec(SeatKind.Bot, $"Bot {index + 1}")),
        ];

        Result<GameState> created = GameEngine.CreateGame(GameId, RoomCode, seats, TestGames.DefaultSeed);
        Assert.Null(created.Error);
        Assert.True(await store.TryAddAsync(created.Value, TestContext.Current.CancellationToken));
    }

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

/// <summary>
/// A store that runs something of the test's choosing in the middle of a room-code lookup.
/// </summary>
/// <remarks>
/// Everything else is delegated untouched, so what is under test is still the real store's
/// behaviour — this only decides <em>when</em> the interleaved command happens, which is the one
/// thing a test otherwise has no way to pin down.
/// </remarks>
internal sealed class Interleaving(IGameStore inner) : IGameStore
{
    public Func<Task>? DuringRoomCodeLookup { get; set; }

    public Task<bool> TryAddAsync(GameState state, CancellationToken cancellationToken = default) =>
        inner.TryAddAsync(state, cancellationToken);

    public Task<StoredGame?> FindAsync(string gameId, CancellationToken cancellationToken = default) =>
        inner.FindAsync(gameId, cancellationToken);

    public async Task<StoredGame?> FindByRoomCodeAsync(
        string roomCode, CancellationToken cancellationToken = default)
    {
        StoredGame? found = await inner.FindByRoomCodeAsync(roomCode, cancellationToken);

        if (DuringRoomCodeLookup is { } interleaved)
        {
            // Once. A second helping would fire on the re-read the fix introduced and make the
            // test pass for the wrong reason, by moving the store on again behind its own back.
            DuringRoomCodeLookup = null;
            await interleaved();
        }

        return found;
    }

    public Task<Result<StoredGame>?> MutateAsync(
        string gameId,
        Func<GameState, Result<GameState>> command,
        CancellationToken cancellationToken = default) =>
        inner.MutateAsync(gameId, command, cancellationToken);
}
