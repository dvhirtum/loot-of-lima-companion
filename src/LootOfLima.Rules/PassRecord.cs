namespace LootOfLima.Rules;

/// <summary>Which way a token travelled around the table.</summary>
/// <remarks>
/// Seat indices ascend clockwise, so <see cref="Right"/> is <c>+1</c>, <see cref="Left"/> is
/// <c>-1</c> and <see cref="Across"/> is <c>+2</c> at four players.
/// </remarks>
public enum PassDirection
{
    Across = 0,
    Right = 1,
    Left = 2,
}

/// <summary>
/// One token handed to one other player at setup. Private to its two endpoints: the giver learns a
/// location the receiver now holds, which is the whole point of passing.
/// </summary>
public sealed record PassRecord(int GiverSeat, int ReceiverSeat, LocationId Location, PassDirection Direction)
{
    public override string ToString() =>
        $"seat {GiverSeat} passed {Location} {Direction} to seat {ReceiverSeat}";
}
