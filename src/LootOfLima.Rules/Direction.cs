namespace LootOfLima.Rules;

/// <summary>The eight compass directions. Declaration order is clockwise starting at North,
/// which the arc arithmetic in <see cref="BoardLayout"/> relies on.</summary>
public enum Direction
{
    N = 0,
    NE = 1,
    E = 2,
    SE = 3,
    S = 4,
    SW = 5,
    W = 6,
    NW = 7,
}

public static class Directions
{
    public const int Count = 8;

    public static readonly IReadOnlyList<Direction> All =
    [
        Direction.N, Direction.NE, Direction.E, Direction.SE,
        Direction.S, Direction.SW, Direction.W, Direction.NW,
    ];

    public static Direction Next(this Direction direction) => (Direction)(((int)direction + 1) % Count);

    public static Direction Previous(this Direction direction) => (Direction)(((int)direction + Count - 1) % Count);

    public static Direction Parse(string text) =>
        Enum.TryParse<Direction>(text.Trim(), ignoreCase: true, out var direction)
            ? direction
            : throw new FormatException($"'{text}' is not a compass direction.");
}
