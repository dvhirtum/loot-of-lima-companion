using System.Text.Json;
using LootOfLima.Contracts;
using LootOfLima.Rules;
using LootOfLima.Server.Games;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace LootOfLima.Server.Tests;

/// <summary>
/// The real application, over a real hub, with real clients.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a mock of anything. The property under test is what a client actually receives,
/// so anything standing in for the transport or the serializer would be testing the stand-in.
/// </para>
/// <para>
/// Every frame each client receives is kept, as raw JSON, because that is what the leak tests need:
/// a substring search over the bytes a viewer was sent is crude, and therefore very hard to fool.
/// </para>
/// </remarks>
internal sealed class HubHarness : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly List<TestClient> _clients = [];

    public string RoomCode { get; } = "TEST";

    public string GameId { get; } = "game-under-test";

    /// <summary>Puts a lobby into the running server's store, ready to be joined.</summary>
    public async Task<GameState> SeedLobbyAsync(int playerCount = 5)
    {
        IReadOnlyList<SeatSpec> seats =
        [
            .. Enumerable.Range(0, playerCount).Select(index => new SeatSpec(SeatKind.Bot, $"Bot {index + 1}")),
        ];

        Result<GameState> created = GameEngine.CreateGame(GameId, RoomCode, seats, TestGames.DefaultSeed);
        Assert.Null(created.Error);

        IGameStore store = _factory.Services.GetRequiredService<IGameStore>();
        Assert.True(await store.TryAddAsync(created.Value, TestContext.Current.CancellationToken));
        return created.Value;
    }

    /// <summary>The game as the server now holds it, for asserting on what the engine actually did.</summary>
    public async Task<GameState> CurrentAsync() => (await StoredAsync()).State;

    /// <summary>The same game with the store's revision of it, for asserting on the version stamp.</summary>
    public async Task<StoredGame> StoredAsync()
    {
        IGameStore store = _factory.Services.GetRequiredService<IGameStore>();
        StoredGame? stored = await store.FindAsync(GameId, TestContext.Current.CancellationToken);
        return stored ?? throw new InvalidOperationException("The seeded game is gone.");
    }

    /// <summary>Issues a token the way joining will, without needing the REST endpoint to exist yet.</summary>
    public string IssueToken(string displayName)
    {
        _factory.Services.GetRequiredService<IPlayerTokenStore>().Issue(GameId, displayName, out string token);
        return token;
    }

    /// <summary>Connects a client, as a player when given a token and as a spectator otherwise.</summary>
    public async Task<TestClient> ConnectAsync(string? playerToken = null)
    {
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "hub/game"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();

                    // TestServer speaks long polling without any of the WebSocket plumbing, and the
                    // transport is not what any of these tests are about.
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

        TestClient client = new(connection, playerToken);
        _clients.Add(client);

        // Handlers before the connection opens, so nothing the server pushes can arrive unheard.
        client.Listen();
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return client;
    }

    /// <summary>
    /// Has one client act, then waits until every other connected client has received the frame it
    /// caused.
    /// </summary>
    /// <remarks>
    /// Every client watching a game is sent a frame whenever anybody acts, but they are sent
    /// independently and arrive independently. The acting client waits for its own reply inside
    /// <see cref="TestClient.InvokeAsync"/>; without this, a test that then read somebody else's
    /// view would be reading whatever had happened to arrive by then, and would pass or fail on
    /// timing rather than on the server being right.
    /// </remarks>
    public async Task ActAsync(TestClient actor, string method, params object?[] arguments)
    {
        int errorsBefore = actor.ErrorCount;
        Dictionary<TestClient, int> before = _clients.ToDictionary(client => client, client => client.FrameCount);

        await actor.InvokeAsync(method, arguments);

        if (actor.ErrorCount > errorsBefore)
        {
            // A rejected command is answered to the caller alone, so nobody else has anything coming.
            return;
        }

        foreach (TestClient client in _clients)
        {
            await client.WaitForFramesAsync(before[client] + 1);
        }
    }

    /// <summary>Connects a client and rejoins it to the seeded game in one step.</summary>
    /// <remarks>
    /// Nobody else is waited for, because nobody else is sent anything: an arrival changes no seat,
    /// no hand and no history, so the server answers the joiner alone. This used to wait for the
    /// whole table, and the frame it waited for was the one a later <see cref="ActAsync"/> would
    /// then mistake for the answer to its own command.
    /// </remarks>
    public async Task<TestClient> JoinAsync(string? playerToken = null)
    {
        TestClient joining = await ConnectAsync(playerToken);
        await joining.InvokeAsync("Rejoin", RoomCode, playerToken);
        return joining;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (TestClient client in _clients)
        {
            await client.DisposeAsync();
        }

        await _factory.DisposeAsync();
    }

}

/// <summary>
/// One connected client, and everything the server has sent it.
/// </summary>
/// <remarks>
/// Everything arrives on a SignalR dispatch thread while the test thread reads it, so all three
/// collections are written and read under one lock and every reader is handed a copy. A list is
/// not safe to add to while somebody else is counting it: growing one puts a new array behind the
/// count, and an assertion that enumerated at the wrong moment could see a torn list or throw for
/// reasons that have nothing to do with the server it was meant to be testing.
/// </remarks>
internal sealed class TestClient(HubConnection connection, string? playerToken) : IAsyncDisposable
{
    private readonly Received<string> _stateFrames = new();
    private readonly Received<ErrorDto> _errors = new();
    private readonly Received<GameEventDto> _events = new();
    private readonly SemaphoreSlim _delivered = new(0);

    public string? PlayerToken { get; } = playerToken;

    /// <summary>Every <c>State</c> payload as raw JSON, oldest first.</summary>
    public IReadOnlyList<string> StateFrames => _stateFrames.Snapshot();

    public IReadOnlyList<ErrorDto> Errors => _errors.Snapshot();

    /// <summary>The nudges saying what changed, which carry no game detail of their own.</summary>
    public IReadOnlyList<GameEventDto> Events => _events.Snapshot();

    /// <summary>How many state frames have arrived, without copying them all to find out.</summary>
    public int FrameCount => _stateFrames.Count;

    public int ErrorCount => _errors.Count;

    public int EventCount => _events.Count;

    /// <summary>The most recent view, parsed. Throws if nothing has arrived, which is itself a result.</summary>
    public PlayerView View => JsonSerializer.Deserialize<PlayerView>(
        _stateFrames.Last() ?? throw new InvalidOperationException("No state has arrived."),
        Json)!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Listen()
    {
        connection.On<JsonElement>("State", frame =>
        {
            _stateFrames.Add(frame.GetRawText());
            _delivered.Release();
        });

        connection.On<ErrorDto>("Error", error =>
        {
            _errors.Add(error);
            _delivered.Release();
        });

        // Deliberately no release. An event accompanies a state frame rather than answering a
        // command by itself, so counting it as an answer would leave InvokeAsync a frame ahead of
        // what actually arrived. Waiting for events has its own method.
        connection.On<GameEventDto>("Event", happened => _events.Add(happened));
    }

    /// <summary>
    /// Calls a hub method and waits for the reply it causes, so a test never races the push.
    /// </summary>
    /// <remarks>
    /// The backlog is drained first. Every client watching a game is sent a frame whenever anybody
    /// acts, so without draining, this would be satisfied by a frame caused by somebody else's
    /// last command and return before this one had been answered at all. A command produces either
    /// a state push or an error and never neither, which is what makes waiting for exactly one
    /// frame correct rather than merely convenient.
    /// </remarks>
    public async Task InvokeAsync(string method, params object?[] arguments)
    {
        while (_delivered.CurrentCount > 0)
        {
            await _delivered.WaitAsync(TestContext.Current.CancellationToken);
        }

        await connection.InvokeCoreAsync(method, arguments, TestContext.Current.CancellationToken);

        bool answered = await _delivered.WaitAsync(
            TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(answered, $"The server never answered {method}.");
    }

    /// <summary>Waits until at least <paramref name="frames"/> state frames have arrived.</summary>
    public Task WaitForFramesAsync(int frames) =>
        WaitForAsync(() => FrameCount, frames, "state frames");

    /// <summary>Waits until at least <paramref name="events"/> nudges have arrived.</summary>
    /// <remarks>
    /// Events ride alongside state rather than in place of it, so they are not what
    /// <see cref="InvokeAsync"/> waits for, and a test that wants one has to say so.
    /// </remarks>
    public Task WaitForEventsAsync(int events) =>
        WaitForAsync(() => EventCount, events, "events");

    private static async Task WaitForAsync(Func<int> arrived, int wanted, string what)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (arrived() < wanted)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Only {arrived()} of {wanted} {what} arrived.");
            await Task.Delay(TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
