namespace LootOfLima.Rules;

/// <summary>The deduction rule the whole game turns on.</summary>
public static class Engine
{
    /// <summary>
    /// Answers "how many of your location tokens are in this query range?" — the single function
    /// proven against every published answer in the official scenario books.
    /// </summary>
    public static int CountInRange(ReadOnlySpan<LocationId> hand, QueryRange range, BoardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var arc = layout.ArcSectorMask(range.Start, range.End);
        var count = 0;

        foreach (var location in hand)
        {
            if (range.TerrainFilter is { } terrain && location.Terrain != terrain)
            {
                continue;
            }

            if ((arc & (1 << (int)layout.SectorOf(location))) != 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc cref="CountInRange(ReadOnlySpan{LocationId}, QueryRange, BoardLayout)"/>
    public static int CountInRange(IReadOnlyCollection<LocationId> hand, QueryRange range, BoardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(hand);
        return CountInRange([.. hand], range, layout);
    }
}
