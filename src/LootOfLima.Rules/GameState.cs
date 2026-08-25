namespace LootOfLima.Rules;

/// <summary>
/// The whole game. This type is the secret: it holds every hand and the treasure, so it must never
/// be serialized to anyone. Clients only ever see what the projection makes of it.
/// </summary>
/// <param name="Deal">Null until the game starts.</param>
/// <param name="ActiveSeat">Whose turn it is; null outside <see cref="GamePhase.InProgress"/>.</param>
/// <param name="TurnNumber">Counts from 1 once play starts, and from 0 before it.</param>
/// <param name="PendingPasses">
/// Passes chosen at deal time but not yet delivered. They wait because a player has to pick up the
/// tokens they were dealt before they can hand one of them over.
/// </param>
/// <param name="RandomSeed">Reproduces a generated deal and its passes.</param>
public sealed record GameState(
    string GameId,
    string RoomCode,
    GamePhase Phase,
    IReadOnlyList<Seat> Seats,
    Deal? Deal,
    int? ActiveSeat,
    int TurnNumber,
    IReadOnlyList<HistoryEntry> History,
    IReadOnlyList<PassRecord> PendingPasses,
    int? WinnerSeat,
    GameEndReason? EndReason,
    int RandomSeed)
{
    public int PlayerCount => Seats.Count;

    public bool IsSeatIndex(int index) => index >= 0 && index < Seats.Count;

    public Seat SeatAt(int index) => IsSeatIndex(index)
        ? Seats[index]
        : throw new ArgumentOutOfRangeException(nameof(index), index, "No such seat in this game.");

    /// <summary>Seats that can take turns and accuse. Bots do neither.</summary>
    public IEnumerable<Seat> HumanSeats => Seats.Where(seat => seat.IsHuman);

    /// <summary>Human seats still in the game.</summary>
    public IEnumerable<Seat> ActiveHumanSeats => HumanSeats.Where(seat => !seat.Eliminated);

    /// <summary>Every token pass that has been delivered, in the order it was made.</summary>
    public IEnumerable<PassRecord> PassRecords => History.OfType<TokenPassEntry>().Select(entry => entry.Pass);
}
