using LootOfLima.Rules;

namespace LootOfLima.Scenarios;

/// <summary>
/// One of the official solo/two-player scenarios, decoded from the printed books. Every scenario
/// is a five-player deal: P1-P5 hold four locations each, two are publicly revealed, two are the
/// treasure.
/// </summary>
/// <param name="Id">Stable identifier, e.g. <c>b1s1</c> for book 1, scenario 1.</param>
/// <param name="Book">Book number, 1-20.</param>
/// <param name="Number">Scenario number within the book, 1-12.</param>
/// <param name="Hands">Hands for seats 0-4 (P1-P5), four locations each.</param>
/// <param name="PublicReveal">The two locations revealed to everyone at setup.</param>
/// <param name="Treasure">The two hidden locations that win the game.</param>
/// <param name="FewestTerrain">Per seat, the terrain that seat holds fewest of. The books print
/// this as a solo aid in place of the physical game's token passing.</param>
public sealed record Scenario(
    string Id,
    int Book,
    int Number,
    IReadOnlyList<IReadOnlyList<LocationId>> Hands,
    IReadOnlyList<LocationId> PublicReveal,
    IReadOnlyList<LocationId> Treasure,
    IReadOnlyList<Terrain?> FewestTerrain)
{
    /// <summary>Every scenario in the books simulates a five-player game.</summary>
    public int PlayerCount => 5;

    public override string ToString() => $"Book {Book}, scenario {Number}";
}
