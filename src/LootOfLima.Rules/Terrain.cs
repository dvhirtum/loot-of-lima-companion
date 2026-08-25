namespace LootOfLima.Rules;

/// <summary>The three terrain types on the island. Note that the blue "wild" die face is a
/// property of the dice, not a terrain, so it deliberately has no member here.</summary>
public enum Terrain
{
    Beach = 0,
    Forest = 1,
    Mountain = 2,
}

public static class Terrains
{
    public static readonly IReadOnlyList<Terrain> All = [Terrain.Beach, Terrain.Forest, Terrain.Mountain];

    /// <summary>The four filters a question can carry: no filter (all terrain), or one terrain.</summary>
    public static readonly IReadOnlyList<Terrain?> AllFilters = [null, Terrain.Beach, Terrain.Forest, Terrain.Mountain];

    public static char ToCode(this Terrain terrain) => terrain switch
    {
        Terrain.Beach => 'B',
        Terrain.Forest => 'F',
        Terrain.Mountain => 'M',
        _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unknown terrain."),
    };

    public static Terrain FromCode(char code) => code switch
    {
        'B' or 'b' => Terrain.Beach,
        'F' or 'f' => Terrain.Forest,
        'M' or 'm' => Terrain.Mountain,
        _ => throw new FormatException($"'{code}' is not a terrain code (expected B, F or M)."),
    };
}
