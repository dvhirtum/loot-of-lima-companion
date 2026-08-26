namespace LootOfLima.Contracts;

/// <summary>Opening a table.</summary>
/// <param name="PlayerCount">Three, four or five seats. Every seat starts as a bot.</param>
public sealed record CreateGameRequest(int PlayerCount);

/// <summary>
/// A new table.
/// </summary>
/// <param name="JoinUrl">
/// What the QR code encodes and what a phone opens. Handed back rather than assembled by the
/// client, because the host's screen is the only thing that knows the address players can reach.
/// </param>
public sealed record CreateGameResponse(string GameId, string RoomCode, string JoinUrl);

/// <summary>
/// A table as somebody deciding whether to join it may see it.
/// </summary>
/// <remarks>
/// Reachable without any credential at all, so it carries nothing but what is written on the
/// outside of the box: how many seats there are and which are taken. No hand, no deal, no history.
/// </remarks>
public sealed record LobbySummary(
    string RoomCode,
    string Phase,
    int PlayerCount,
    IReadOnlyList<LobbySeat> Seats);

/// <summary>One seat, as the lobby shows it.</summary>
/// <param name="Taken">Whether a person is in it. A free seat is being played by a bot until then.</param>
public sealed record LobbySeat(int Index, string DisplayName, bool Taken);

/// <summary>Sitting down at a table.</summary>
public sealed record JoinRequest(string DisplayName);

/// <summary>
/// Proof of identity for one player at one table.
/// </summary>
/// <param name="PlayerToken">
/// A bearer credential: whoever holds it holds the seat and the hand that comes with it. It is
/// kept in the player's own browser and never appears in anybody else's view.
/// </param>
/// <param name="PlayerId">
/// Who the server knows them as. This one does travel — it is written onto a seat — which is why
/// it is not derived from the token.
/// </param>
public sealed record JoinResponse(string PlayerToken, string PlayerId);

/// <summary>One of the official printed scenarios.</summary>
/// <param name="PlayerCount">Always five: the books are five-player puzzles.</param>
public sealed record ScenarioSummary(string Id, int Book, int Number, int PlayerCount);

/// <summary>
/// The island, so the map and the engine cannot disagree about it.
/// </summary>
/// <remarks>
/// Served rather than duplicated in the client for exactly that reason: the geometry was recovered
/// by fitting it against the published books, and a second copy drawn by hand would be a second
/// chance to get it wrong.
/// </remarks>
public sealed record BoardLayoutDto(IReadOnlyList<BoardSector> Sectors, ArcSemantics Arc);

/// <summary>One compass sector and the three locations standing in it.</summary>
public sealed record BoardSector(string Direction, IReadOnlyList<string> Locations);

/// <summary>
/// How a question's arc is read.
/// </summary>
/// <remarks>
/// Spelled out on the wire because it is the subtle part of the game: the dial's arrows point at
/// the eight compass lines, not at sectors, and tokens sit in the gaps between the lines.
/// </remarks>
/// <param name="StartEqualsEndMeansFullCircle">Coincident arrows sweep all eight sectors, not none.</param>
public sealed record ArcSemantics(
    bool StartInclusive,
    bool EndExclusive,
    bool StartEqualsEndMeansFullCircle);
