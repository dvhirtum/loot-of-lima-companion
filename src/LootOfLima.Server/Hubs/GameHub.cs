using LootOfLima.Contracts;
using LootOfLima.Rules;
using LootOfLima.Scenarios;
using LootOfLima.Server.Games;
using Microsoft.AspNetCore.SignalR;

namespace LootOfLima.Server.Hubs;

/// <summary>
/// Every move a player can make.
/// </summary>
/// <remarks>
/// <para>
/// Two rules hold everywhere in this class. First, no method returns game state: state reaches a
/// client only through <see cref="GameBroadcaster"/>, which projects per connection. Second, who is
/// calling is resolved from the connection's identity and never from the request — a client that
/// could name its own seat could name somebody else's.
/// </para>
/// <para>
/// Rule violations are answered with <c>Error</c> to the one caller, never thrown and never
/// broadcast: which move somebody just tried and failed is not the table's business.
/// </para>
/// </remarks>
internal sealed class GameHub(
    IGameStore games,
    IPlayerTokenStore tokens,
    IConnectionRegistry connections,
    GameBroadcaster broadcaster) : Hub
{
    internal const string ErrorMethod = "Error";

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Detach(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Attaches this connection to a game, as a player if it presents a token and as a spectator
    /// otherwise. Idempotent, and safe from several devices holding the same token: each connection
    /// is bound separately and every one of them projects to the same seat.
    /// </summary>
    public async Task Rejoin(string roomCode, string? playerToken)
    {
        GameState? game = await games.FindByRoomCodeAsync(roomCode, Context.ConnectionAborted).ConfigureAwait(false);
        if (game is null)
        {
            await FailAsync("NoSuchGame", $"No live game has the code {roomCode}.").ConfigureAwait(false);
            return;
        }

        PlayerIdentity? identity = tokens.Resolve(playerToken);
        if (playerToken is not null
            && !string.Equals(identity?.GameId, game.GameId, StringComparison.Ordinal))
        {
            // A token from another table is not a spectator pass. Silently downgrading it would
            // leave a player staring at a hand-less view with nothing to explain why.
            await FailAsync("UnknownPlayerToken", "That token was not issued for this game.").ConfigureAwait(false);
            return;
        }

        connections.Attach(new Connection(Context.ConnectionId, game.GameId, identity));
        await broadcaster.BroadcastAsync(game, Context.ConnectionAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes a seat. The seat index comes from the client, because choosing where to sit is the
    /// player's business; the identity written onto that seat never does.
    /// </summary>
    public Task ClaimSeat(int seatIndex) =>
        WithPlayerAsync(
            (player, state) => GameEngine.ClaimSeat(state, seatIndex, player.PlayerId, player.DisplayName),
            new GameEventDto("SeatClaimed", seatIndex));

    public Task ReleaseSeat() =>
        WithSeatAsync(
            (seat, state) => GameEngine.ReleaseSeat(state, seat),
            seat => new GameEventDto("SeatReleased", seat));

    /// <summary>Deals and begins. Any seated player may start; the app always owns the deal.</summary>
    public async Task StartGame(StartGameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ScenarioId is { } id && !ScenarioLibrary.Ids.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            await FailAsync("NoSuchScenario", $"There is no scenario '{id}'.").ConfigureAwait(false);
            return;
        }

        await ApplyAsync(
            state => GameEngine.Start(
                state,
                request.ScenarioId is { } scenarioId
                    ? ScenarioLibrary.Get(scenarioId).ToDeal()
                    : Dealer.Create(state.PlayerCount, state.RandomSeed),
                new DeterministicShuffleSource(state.RandomSeed)),
            new GameEventDto("GameStarted")).ConfigureAwait(false);
    }

    public Task ConfirmTokensTaken() =>
        WithSeatAsync((seat, state) => GameEngine.ConfirmTokensTaken(state, seat), _ => null);

    public Task ConfirmPassDone() =>
        WithSeatAsync((seat, state) => GameEngine.ConfirmPassDone(state, seat), _ => null);

    public async Task AnnounceFewestTerrain(string terrain)
    {
        if (!Enum.TryParse(terrain, ignoreCase: true, out Terrain announced))
        {
            await FailAsync("BadRequest", $"'{terrain}' is not a terrain.").ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.AnnounceFewestTerrain(state, seat, announced),
            seat => new GameEventDto("FewestTerrainAnnounced", seat)).ConfigureAwait(false);
    }

    public async Task SpendAbility(string ability)
    {
        if (!Enum.TryParse(ability, ignoreCase: true, out AbilityType spent))
        {
            await FailAsync("BadRequest", $"'{ability}' is not an ability.").ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.SpendAbility(state, seat, spent),
            seat => new GameEventDto("AbilitySpent", seat)).ConfigureAwait(false);
    }

    public async Task AskQuestion(AskQuestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryReadRange(request.Start, request.End, request.Terrain, out QueryRange range, out string? complaint))
        {
            await FailAsync("BadRequest", complaint).ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.AskQuestion(state, seat, request.TargetSeat, range, BoardLayout.Default),
            seat => new GameEventDto("QuestionAsked", seat)).ConfigureAwait(false);
    }

    /// <summary>
    /// A Pistol question. The request has no terrain property at all, so the rule that a secret
    /// question is never terrain-restricted is enforced by the shape of the call rather than by a
    /// check somebody could forget to write.
    /// </summary>
    public async Task AskSecretQuestion(AskSecretQuestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryReadRange(request.Start, request.End, null, out QueryRange range, out string? complaint))
        {
            await FailAsync("BadRequest", complaint).ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.AskSecretQuestion(state, seat, request.TargetSeat, range, BoardLayout.Default),
            seat => new GameEventDto("QuestionAsked", seat)).ConfigureAwait(false);
    }

    public async Task Accuse(AccuseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryReadLocation(request.First, out LocationId first, out string? complaint)
            || !TryReadLocation(request.Second, out LocationId second, out complaint))
        {
            await FailAsync("BadRequest", complaint).ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.Accuse(state, seat, first, second),
            seat => new GameEventDto("AccusationMade", seat)).ConfigureAwait(false);
    }

    private static bool TryReadRange(
        string start,
        string end,
        string? terrain,
        out QueryRange range,
        out string? complaint)
    {
        range = default;
        complaint = null;

        if (!Enum.TryParse(start, ignoreCase: true, out Direction from))
        {
            complaint = $"'{start}' is not a compass direction.";
            return false;
        }

        if (!Enum.TryParse(end, ignoreCase: true, out Direction to))
        {
            complaint = $"'{end}' is not a compass direction.";
            return false;
        }

        Terrain? filter = null;
        if (terrain is not null)
        {
            if (!Enum.TryParse(terrain, ignoreCase: true, out Terrain parsed))
            {
                complaint = $"'{terrain}' is not a terrain.";
                return false;
            }

            filter = parsed;
        }

        range = new QueryRange(from, to, filter);
        return true;
    }

    private static bool TryReadLocation(string text, out LocationId location, out string? complaint)
    {
        location = default;
        complaint = null;

        foreach (LocationId candidate in LocationId.All)
        {
            if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                location = candidate;
                return true;
            }
        }

        complaint = $"'{text}' is not one of the 24 island locations.";
        return false;
    }

    private static int? SeatOf(GameState state, string playerId) =>
        state.Seats.FirstOrDefault(
            seat => string.Equals(seat.PlayerId, playerId, StringComparison.Ordinal))?.Index;

    /// <summary>Runs a command that needs to know who is calling, but not where they are sitting.</summary>
    private Task WithPlayerAsync(Func<PlayerIdentity, GameState, Result<GameState>> command, GameEventDto? happened)
    {
        if (connections.Find(Context.ConnectionId)?.Player is not { } player)
        {
            return FailAsync("NotAPlayer", "Join the game before taking a seat.");
        }

        return ApplyAsync(state => command(player, state), happened);
    }

    /// <summary>
    /// Runs a command on behalf of the caller's seat. The seat is looked up inside the mutation, so
    /// it is read from the very state the command is then applied to rather than from an earlier one.
    /// </summary>
    private Task WithSeatAsync(Func<int, GameState, Result<GameState>> command, Func<int, GameEventDto?> happened)
    {
        if (connections.Find(Context.ConnectionId)?.Player is not { } player)
        {
            return FailAsync("NotAPlayer", "Only a seated player can do that.");
        }

        GameEventDto? nudge = null;
        return ApplyAsync(
            state =>
            {
                if (SeatOf(state, player.PlayerId) is not { } seat)
                {
                    return new GameError(GameErrorCode.SeatNotClaimed, "You are not sitting at this table.");
                }

                nudge = happened(seat);
                return command(seat, state);
            },
            () => nudge);
    }

    private Task ApplyAsync(Func<GameState, Result<GameState>> command, GameEventDto? happened) =>
        ApplyAsync(command, () => happened);

    private async Task ApplyAsync(Func<GameState, Result<GameState>> command, Func<GameEventDto?> happened)
    {
        if (connections.Find(Context.ConnectionId) is not { } connection)
        {
            await FailAsync("NotInAGame", "Rejoin before sending commands.").ConfigureAwait(false);
            return;
        }

        Result<GameState>? outcome = await games
            .MutateAsync(connection.GameId, command, Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (outcome is not { } result)
        {
            await FailAsync("NoSuchGame", "This game is no longer live.").ConfigureAwait(false);
            return;
        }

        if (result.Error is { } error)
        {
            await FailAsync(error.Code.ToString(), error.Message).ConfigureAwait(false);
            return;
        }

        if (happened() is { } nudge)
        {
            await broadcaster.BroadcastAsync(result.Value, nudge, Context.ConnectionAborted).ConfigureAwait(false);
        }
        else
        {
            await broadcaster.BroadcastAsync(result.Value, Context.ConnectionAborted).ConfigureAwait(false);
        }
    }

    /// <summary>Answers the one caller. Errors are addressed, never broadcast.</summary>
    private Task FailAsync(string code, string? message) =>
        Clients.Caller.SendAsync(ErrorMethod, new ErrorDto(code, message ?? code), Context.ConnectionAborted);
}
