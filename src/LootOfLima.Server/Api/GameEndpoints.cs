using LootOfLima.Contracts;
using LootOfLima.Rules;
using LootOfLima.Scenarios;
using LootOfLima.Server.Games;
using Microsoft.AspNetCore.Http.HttpResults;
using QRCoder;

namespace LootOfLima.Server.Api;

/// <summary>
/// The handful of things that happen before a game has a connection: opening a table, looking one
/// up, and sitting down at it.
/// </summary>
/// <remarks>
/// Everything after that is the hub's, because everything after that is game state, and game state
/// leaves this server through exactly one function. Nothing here ever touches a hand, a deal or a
/// history — the lobby summary is what is written on the outside of the box.
/// </remarks>
internal static class GameEndpoints
{
    /// <summary>How many room codes to try before giving up on finding a free one.</summary>
    /// <remarks>
    /// The alphabet gives 331,776 codes, so a collision means an unusually busy evening rather than
    /// a broken generator, and a handful of attempts is plenty. Looping forever would turn a full
    /// table of live games into a hung request.
    /// </remarks>
    private const int RoomCodeAttempts = 8;

    public static void MapGameEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/games", CreateGameAsync);
        app.MapGet("/api/games/{roomCode}", LobbyAsync);
        app.MapPost("/api/games/{roomCode}/join", JoinAsync);
        app.MapGet("/api/games/{roomCode}/qr.png", QrCodeAsync);
        app.MapGet("/api/scenarios", Scenarios);
        app.MapGet("/api/board", Board);
    }

    private static async Task<Results<Created<CreateGameResponse>, BadRequest<ErrorDto>>> CreateGameAsync(
        CreateGameRequest request,
        HttpRequest http,
        IGameStore games,
        CancellationToken cancellationToken)
    {
        // Asked before the seats are built rather than after. The engine still owns the rule — this
        // is its own predicate, not a second copy of the numbers — but CreateGame learns the count
        // from the list it is handed, and a list is the one thing that cannot be built first: a
        // negative count throws out of Enumerable.Range, and a huge one allocates for a long time
        // before being told it was never a table.
        if (!DealTable.IsSupportedPlayerCount(request.PlayerCount))
        {
            return TypedResults.BadRequest(new ErrorDto(
                GameErrorCode.UnsupportedPlayerCount.ToString(),
                $"Loot of Lima is for {DealTable.MinPlayers}-{DealTable.MaxPlayers} players, "
                + $"not {request.PlayerCount}."));
        }

        // Every seat starts as a bot, and players turn them human by claiming them. A table is
        // therefore playable the moment it exists, which is what lets one person start a scenario.
        IReadOnlyList<SeatSpec> seats =
        [
            .. Enumerable.Range(0, request.PlayerCount)
                .Select(index => new SeatSpec(SeatKind.Bot, $"Bot {index + 1}")),
        ];

        for (int attempt = 0; attempt < RoomCodeAttempts; attempt++)
        {
            Result<GameState> created = GameEngine.CreateGame(
                Guid.NewGuid().ToString("n"), RoomCodes.Next(), seats, Random.Shared.Next());

            if (created.Error is { } error)
            {
                return TypedResults.BadRequest(new ErrorDto(error.Code.ToString(), error.Message));
            }

            if (await games.TryAddAsync(created.Value, cancellationToken).ConfigureAwait(false))
            {
                GameState game = created.Value;
                return TypedResults.Created(
                    $"/api/games/{game.RoomCode}",
                    new CreateGameResponse(game.GameId, game.RoomCode, JoinUrl(http, game.RoomCode)));
            }
        }

        return TypedResults.BadRequest(new ErrorDto(
            "NoRoomCodeAvailable", "Could not find a free room code. Try again."));
    }

    private static async Task<Results<Ok<LobbySummary>, NotFound>> LobbyAsync(
        string roomCode,
        IGameStore games,
        CancellationToken cancellationToken)
    {
        StoredGame? game = await games.FindByRoomCodeAsync(roomCode, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new LobbySummary(
            game.State.RoomCode,
            game.State.Phase.ToString(),
            game.State.PlayerCount,
            [.. game.State.Seats.Select(seat => new LobbySeat(seat.Index, seat.DisplayName, seat.IsHuman))]));
    }

    private static async Task<Results<Ok<JoinResponse>, NotFound, BadRequest<ErrorDto>>> JoinAsync(
        string roomCode,
        JoinRequest request,
        IGameStore games,
        IPlayerTokenStore tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return TypedResults.BadRequest(new ErrorDto("BadRequest", "A player needs a name."));
        }

        StoredGame? game = await games.FindByRoomCodeAsync(roomCode, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        // Joining issues an identity but claims no seat: which seat is the player's own choice, and
        // it is made over the hub where the whole table can watch the lobby fill up.
        PlayerIdentity identity = tokens.Issue(game.State.GameId, request.DisplayName.Trim(), out string token);
        return TypedResults.Ok(new JoinResponse(token, identity.PlayerId));
    }

    private static async Task<Results<FileContentHttpResult, NotFound>> QrCodeAsync(
        string roomCode,
        HttpRequest http,
        IGameStore games,
        CancellationToken cancellationToken)
    {
        StoredGame? game = await games.FindByRoomCodeAsync(roomCode, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(
            JoinUrl(http, game.State.RoomCode), QRCodeGenerator.ECCLevel.Q);

        // Rendered server-side so the host's screen needs nothing but an img tag, and so the URL
        // in the code is the one the server believes in rather than one a page assembled.
        return TypedResults.File(new PngByteQRCode(data).GetGraphic(pixelsPerModule: 8), "image/png");
    }

    private static Ok<IReadOnlyList<ScenarioSummary>> Scenarios() =>
        TypedResults.Ok<IReadOnlyList<ScenarioSummary>>(
        [
            .. ScenarioLibrary.All.Select(
                scenario => new ScenarioSummary(
                    scenario.Id, scenario.Book, scenario.Number, scenario.PlayerCount)),
        ]);

    private static Ok<BoardLayoutDto> Board()
    {
        BoardLayout layout = BoardLayout.Default;

        IReadOnlyList<BoardSector> sectors =
        [
            .. Directions.All.Select(direction => new BoardSector(
                direction.ToString(),
                [
                    .. LocationId.All
                        .Where(location => layout.SectorOf(location) == direction)
                        .Select(location => location.ToString()),
                ])),
        ];

        return TypedResults.Ok(new BoardLayoutDto(
            sectors,
            new ArcSemantics(StartInclusive: true, EndExclusive: true, StartEqualsEndMeansFullCircle: true)));
    }

    /// <summary>Where a phone should point to join. Built from the request, so it works on a LAN
    /// address as readily as on a hostname.</summary>
    private static string JoinUrl(HttpRequest http, string roomCode) =>
        $"{http.Scheme}://{http.Host}/join/{roomCode}";
}
