namespace LootOfLima.Rules;

/// <summary>A question's query range: a clockwise arc from <paramref name="Start"/> to
/// <paramref name="End"/>, optionally narrowed to a single terrain.</summary>
/// <remarks>
/// The arc is half-open: the start sector is included and the end sector is excluded. When
/// <c>Start == End</c> the arc sweeps the whole circle. This is because the dial's two arrows point
/// at the eight compass lines rather than at sectors, and tokens sit in the gaps between lines.
/// </remarks>
public readonly record struct QueryRange(Direction Start, Direction End, Terrain? TerrainFilter = null)
{
    public override string ToString()
    {
        var terrain = TerrainFilter is { } t ? t.ToString() : "any terrain";
        return $"{Start}->{End} ({terrain})";
    }
}
