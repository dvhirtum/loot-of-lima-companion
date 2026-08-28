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
        if (await MissingAsync(roomCode).ConfigureAwait(false))
        {
            return;
        }

        StoredGame? game = await games.FindByRoomCodeAsync(roomCode, Context.ConnectionAborted).ConfigureAwait(false);
        if (game is null)
        {
            await FailAsync("NoSuchGame", $"No live game has the code {roomCode}.").ConfigureAwait(false);
            return;
        }

        PlayerIdentity? identity = tokens.Resolve(playerToken);
        if (playerToken is not null
            && !string.Equals(identity?.GameId, game.State.GameId, StringComparison.Ordinal))
        {
            // A token from another table is not a spectator pass. Silently downgrading it would
            // leave a player staring at a hand-less view with nothing to explain why.
            await FailAsync("UnknownPlayerToken", "That token was not issued for this game.").ConfigureAwait(false);
            return;
        }

        // Attached before the state to send is read, and deliberately in that order. A command
        // committing in between broadcasts to the connections it can see, so anything that commits
        // before this line is missed by this connection and anything after it is delivered to it.
        // Reading afterwards closes the gap: whatever was missed is already in what is read here.
        connections.Attach(new Connection(Context.ConnectionId, game.State.GameId, identity));

        StoredGame? current = await games
            .FindAsync(game.State.GameId, Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (current is null)
        {
            // Torn down between the two reads. Nothing to send, and nothing to be attached to.
            connections.Detach(Context.ConnectionId);
            await FailAsync("NoSuchGame", $"No live game has the code {roomCode}.").ConfigureAwait(false);
            return;
        }

        // To the arriving connection alone. Nobody else's view of the game has changed, and telling
        // the table would mean pushing a state this connection read for itself over everybody
        // else's. A frame this send crosses with is sorted out by the version each carries.
        await broadcaster.SendCurrentAsync(current, Context.ConnectionId).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes a seat. The seat index comes from the client, because choosing where to sit is the
    /// player's business; the identity written onto that seat never does.
    /// </summary>
    public Task ClaimSeat(int seatIndex) =>
        WithPlayerAsync(
            (player, state) => GameEngine.ClaimSeat(state, seatIndex, player.PlayerId, player.DisplayName),
            new GameEventDto(GameEventDto.SeatClaimed, seatIndex));

    public Task ReleaseSeat() =>
        WithSeatAsync(
            (seat, state) => GameEngine.ReleaseSeat(state, seat),
            seat => new GameEventDto(GameEventDto.SeatReleased, seat));

    /// <summary>
    /// Deals and begins. Any seated player may start; the app always owns the deal.
    /// </summary>
    /// <remarks>
    /// Seated, and not merely present: a spectator is the host's screen or somebody watching over a
    /// shoulder, and dealing the game and choosing the scenario belong to the people playing it.
    /// </remarks>
    public async Task StartGame(StartGameRequest request)
    {
        if (await MissingAsync(request).ConfigureAwait(false))
        {
            return;
        }

        string? scenario = null;
        if (request.ScenarioId is { } asked)
        {
            // Matched without regard to case, then carried on as the library spells it. The library
            // looks up ordinally, so an id accepted by a lenient check and passed on as the client
            // wrote it would throw from the lookup instead of coming back as an error a client
            // can read.
            scenario = ScenarioLibrary.Ids.FirstOrDefault(
                known => string.Equals(known, asked, StringComparison.OrdinalIgnoreCase));

            if (scenario is null)
            {
                await FailAsync("NoSuchScenario", $"There is no scenario '{asked}'.").ConfigureAwait(false);
                return;
            }
        }

        await WithSeatAsync(
            (_, state) => GameEngine.Start(
                state,
                scenario is not null
                    ? ScenarioLibrary.Get(scenario).ToDeal()
                    : Dealer.Create(state.PlayerCount, state.RandomSeed),
                new DeterministicShuffleSource(state.RandomSeed)),
            _ => new GameEventDto(GameEventDto.GameStarted)).ConfigureAwait(false);
    }

    public Task ConfirmTokensTaken() =>
        WithSeatAsync((seat, state) => GameEngine.ConfirmTokensTaken(state, seat), _ => null);

    public Task ConfirmPassDone() =>
        WithSeatAsync((seat, state) => GameEngine.ConfirmPassDone(state, seat), _ => null);

    public async Task AnnounceFewestTerrain(string terrain)
    {
        if (!TryReadEnum(terrain, out Terrain announced))
        {
            await FailAsync("BadRequest", $"'{terrain}' is not a terrain.").ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.AnnounceFewestTerrain(state, seat, announced),
            seat => new GameEventDto(GameEventDto.FewestTerrainAnnounced, seat)).ConfigureAwait(false);
    }

    public async Task SpendAbility(string ability)
    {
        if (!TryReadEnum(ability, out AbilityType spent))
        {
            await FailAsync("BadRequest", $"'{ability}' is not an ability.").ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.SpendAbility(state, seat, spent),
            seat => new GameEventDto(GameEventDto.AbilitySpent, seat)).ConfigureAwait(false);
    }

    public async Task AskQuestion(AskQuestionRequest request)
    {
        if (await MissingAsync(request).ConfigureAwait(false))
        {
            return;
        }

        if (!TryReadRange(request.Start, request.End, request.Terrain, out QueryRange range, out string? complaint))
        {
            await FailAsync("BadRequest", complaint).ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.AskQuestion(state, seat, request.TargetSeat, range, BoardLayout.Default),
            seat => new GameEventDto(GameEventDto.QuestionAsked, seat)).ConfigureAwait(false);
    }

    /// <summary>
    /// A Pistol question. It reads the same range an ordinary question does, terrain included: the
    /// ability lifts the roll, not the rules about what a question may ask.
    /// </summary>
    public async Task AskSecretQuestion(AskSecretQuestionRequest request)
    {
        if (await MissingAsync(request).ConfigureAwait(false))
        {
            return;
        }

        if (!TryReadRange(request.Start, request.End, request.Terrain, out QueryRange range, out string? complaint))
        {
            await FailAsync("BadRequest", complaint).ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.AskSecretQuestion(state, seat, request.TargetSeat, range, BoardLayout.Default),
            seat => new GameEventDto(GameEventDto.QuestionAsked, seat)).ConfigureAwait(false);
    }

    public async Task Accuse(AccuseRequest request)
    {
        if (await MissingAsync(request).ConfigureAwait(false))
        {
            return;
        }

        if (!TryReadLocation(request.First, out LocationId first, out string? complaint)
            || !TryReadLocation(request.Second, out LocationId second, out complaint))
        {
            await FailAsync("BadRequest", complaint).ConfigureAwait(false);
            return;
        }

        await WithSeatAsync(
            (seat, state) => GameEngine.Accuse(state, seat, first, second),
            seat => new GameEventDto(GameEventDto.AccusationMade, seat)).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends a solo game and reveals the treasure, on the request of the only player in it.
    /// </summary>
    /// <remarks>
    /// No event accompanies it, for the same reason no other ending has one: the view arriving
    /// alongside carries the phase and the treasure, and an event is a nudge to animate, never a
    /// source of state. Every condition on it lives in the engine — that the table has one player,
    /// and that the player has already accused and been wrong — so a client that never draws the
    /// button is a convenience rather than the enforcement.
    /// </remarks>
    public Task GiveUp() =>
        WithSeatAsync((seat, state) => GameEngine.GiveUp(state, seat), _ => null);

    /// <summary>
    /// Reads an enum the client named. Names only, and only names this build knows.
    /// </summary>
    /// <remarks>
    /// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> alone is not enough twice over.
    /// It accepts the underlying number whether or not any member has it, so <c>"8"</c> would arrive
    /// as a ninth compass direction and index off the end of the arc table; and it accepts the
    /// number of a member that does exist, so <c>"0"</c> would be a second spelling of North on a
    /// wire whose contract is names. Checking that the text is the member's own name closes both:
    /// nothing numeric can round-trip through <see cref="Enum.GetName{TEnum}(TEnum)"/>.
    /// </remarks>
    private static bool TryReadEnum<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        return text is not null
            && Enum.TryParse(text, ignoreCase: true, out value)
            && Enum.IsDefined(value)
            && string.Equals(Enum.GetName(value), text.Trim(), StringComparison.OrdinalIgnoreCase);
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

        if (!TryReadEnum(start, out Direction from))
        {
            complaint = $"'{start}' is not a compass direction.";
            return false;
        }

        if (!TryReadEnum(end, out Direction to))
        {
            complaint = $"'{end}' is not a compass direction.";
            return false;
        }

        Terrain? filter = null;
        if (terrain is not null)
        {
            if (!TryReadEnum(terrain, out Terrain parsed))
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

        Result<StoredGame>? outcome = await games
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
            await broadcaster.BroadcastAsync(result.Value, nudge).ConfigureAwait(false);
        }
        else
        {
            await broadcaster.BroadcastAsync(result.Value).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Answers a caller that left out an argument, and says whether it did.
    /// </summary>
    /// <remarks>
    /// Everything here arrives from a client, so a missing argument is a bad request rather than a
    /// bug in this server. Throwing would reach the caller as a HubException reading "An unexpected
    /// error occurred", which is both untrue and no use to whoever has to fix their client, and it
    /// would be the one input in the whole hub that does not come back shaped like every other
    /// refusal.
    /// </remarks>
    private async Task<bool> MissingAsync(object? argument)
    {
        if (argument is not null)
        {
            return false;
        }

        await FailAsync("BadRequest", "That command arrived with a missing argument.")
            .ConfigureAwait(false);

        return true;
    }

    /// <summary>Answers the one caller. Errors are addressed, never broadcast.</summary>
    private Task FailAsync(string code, string? message) =>
        Clients.Caller.SendAsync(ErrorMethod, new ErrorDto(code, message ?? code), Context.ConnectionAborted);
}
