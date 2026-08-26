namespace LootOfLima.Contracts;

/// <summary>
/// Everything one viewer is entitled to know, and nothing else. This is the only shape of game
/// state that ever leaves the server.
/// </summary>
/// <remarks>
/// Locations are plain strings such as <c>B1</c> or <c>M8</c> rather than a shared identifier type,
/// because this assembly deliberately cannot reference the rules engine. The happy side effect is
/// that a leak test can search raw frames for a location by substring.
/// </remarks>
/// <param name="Phase">One of Lobby, Dealing, Passing, Announcing, InProgress, Finished.</param>
/// <param name="ViewerSeat">The seat this view was built for, or null for a spectator.</param>
/// <param name="PublicReveal">The face-up locations. Public from the deal onwards.</param>
/// <param name="Treasure">
/// The two hidden locations. Null in every phase but Finished — this field is the one the whole
/// hidden-information design exists to protect.
/// </param>
/// <param name="EndReason">One of CorrectAccusation, LastPlayerStanding, AllPlayersEliminated.</param>
/// <param name="Version">
/// Which revision of the game this view was built from. It rises by one per command applied, so
/// a client that keeps the highest it has seen can drop a frame that arrives after a later one:
/// commands are ordered, but the sends that follow them are not, and a frame can overtake its
/// successor on the way out. Zero on a view nothing stamped, which is every view built outside a
/// running server.
/// </param>
public sealed record PlayerView(
    string GameId,
    string RoomCode,
    string Phase,
    int PlayerCount,
    int? ViewerSeat,
    int? ActiveSeat,
    int TurnNumber,
    IReadOnlyList<SeatView> Seats,
    IReadOnlyList<string> PublicReveal,
    IReadOnlyList<string>? Treasure,
    IReadOnlyList<HistoryEntryView> History,
    int? WinnerSeat,
    string? EndReason,
    long Version = 0);
