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
/// <param name="Phase">One of Lobby, Dealing, Passing, InProgress, Finished.</param>
/// <param name="ViewerSeat">The seat this view was built for, or null for a spectator.</param>
/// <param name="PublicReveal">The face-up locations. Public from the deal onwards.</param>
/// <param name="Treasure">
/// The two hidden locations. Null in every phase but Finished — this field is the one the whole
/// hidden-information design exists to protect.
/// </param>
/// <param name="EndReason">One of CorrectAccusation, LastPlayerStanding, AllPlayersEliminated.</param>
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
    string? EndReason);
