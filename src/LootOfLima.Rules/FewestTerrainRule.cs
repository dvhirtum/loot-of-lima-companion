namespace LootOfLima.Rules;

/// <summary>
/// The setup announcement: before play, each player names the terrain they hold fewest of.
/// </summary>
/// <remarks>
/// Holding none of a terrain counts as fewest. Where two terrains tie, the rulebook gives the
/// choice to the player and says the tie itself must not be revealed, so this class only ever
/// reports which terrains are eligible — it never picks one, and nothing outside the announcing
/// seat is told how many were eligible.
/// </remarks>
public static class FewestTerrainRule
{
    /// <summary>Every terrain the hand holds fewest of. One entry, or two when they tie.</summary>
    public static IReadOnlyList<Terrain> Minima(IReadOnlyList<LocationId> hand)
    {
        ArgumentNullException.ThrowIfNull(hand);

        int[] counts = new int[Terrains.All.Count];
        foreach (LocationId location in hand)
        {
            counts[(int)location.Terrain]++;
        }

        int fewest = counts.Min();
        return [.. Terrains.All.Where(terrain => counts[(int)terrain] == fewest)];
    }

    /// <summary>Whether a hand could truthfully announce this terrain.</summary>
    public static bool IsTruthful(IReadOnlyList<LocationId> hand, Terrain terrain) =>
        Minima(hand).Contains(terrain);
}
