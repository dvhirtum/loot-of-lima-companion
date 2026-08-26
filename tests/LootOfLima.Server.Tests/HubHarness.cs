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
    public async Task<GameState> CurrentAsync()
    {
        IGameStore store = _factory.Services.GetRequiredService<IGameStore>();
        GameState? state = await store.FindAsync(GameId, TestContext.Current.CancellationToken);
        return state ?? throw new InvalidOperationException("The seeded game is gone.");
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

    /// <summary>Connects a client and rejoins it to the seeded game in one step.</summary>
    public async Task<TestClient> JoinAsync(string? playerToken = null)
    {
        TestClient client = await ConnectAsync(playerToken);
        await client.InvokeAsync("Rejoin", RoomCode, playerToken);
        return client;
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

/// <summary>One connected client, and everything the server has sent it.</summary>
internal sealed class TestClient(HubConnection connection, string? playerToken) : IAsyncDisposable
{
    private readonly List<string> _stateFrames = [];
    private readonly List<ErrorDto> _errors = [];
    private readonly List<GameEventDto> _events = [];
    private readonly SemaphoreSlim _delivered = new(0);

    public string? PlayerToken { get; } = playerToken;

    /// <summary>Every <c>State</c> payload as raw JSON, oldest first.</summary>
    public IReadOnlyList<string> StateFrames => _stateFrames;

    public IReadOnlyList<ErrorDto> Errors => _errors;

    public IReadOnlyList<GameEventDto> Events => _events;

    /// <summary>The most recent view, parsed. Throws if nothing has arrived, which is itself a result.</summary>
    public PlayerView View => JsonSerializer.Deserialize<PlayerView>(
        _stateFrames.Count > 0 ? _stateFrames[^1] : throw new InvalidOperationException("No state has arrived."),
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

        connection.On<GameEventDto>("Event", happened => _events.Add(happened));
    }

    /// <summary>
    /// Calls a hub method and waits for the reply it causes, so a test never races the push.
    /// </summary>
    /// <remarks>
    /// A command produces either a state push or an error, never neither: that is what makes
    /// waiting for one frame correct rather than merely convenient.
    /// </remarks>
    public async Task InvokeAsync(string method, params object?[] arguments)
    {
        await connection.InvokeCoreAsync(method, arguments, TestContext.Current.CancellationToken);

        bool answered = await _delivered.WaitAsync(
            TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(answered, $"The server never answered {method}.");
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
