using System.Diagnostics.CodeAnalysis;

namespace LootOfLima.Rules;

/// <summary>One of the 24 island locations, e.g. Beach 1 (<c>B1</c>) or Mountain 8 (<c>M8</c>).</summary>
/// <param name="Terrain">Which of the three terrains.</param>
/// <param name="Number">1-based index within the terrain, 1..8.</param>
public readonly record struct LocationId(Terrain Terrain, int Number) : IComparable<LocationId>
{
    /// <summary>Orders by <see cref="Index"/>, i.e. by terrain then number.</summary>
    public int CompareTo(LocationId other) => Index.CompareTo(other.Index);

    public static bool operator <(LocationId left, LocationId right) => left.CompareTo(right) < 0;

    public static bool operator <=(LocationId left, LocationId right) => left.CompareTo(right) <= 0;

    public static bool operator >(LocationId left, LocationId right) => left.CompareTo(right) > 0;

    public static bool operator >=(LocationId left, LocationId right) => left.CompareTo(right) >= 0;

    public const int TotalCount = 24;
    public const int PerTerrain = 8;

    /// <summary>Dense 0..23 index, used for bitmasks and array lookups.</summary>
    public int Index => ((int)Terrain * PerTerrain) + (Number - 1);

    public static LocationId FromIndex(int index) =>
        index is >= 0 and < TotalCount
            ? new LocationId((Terrain)(index / PerTerrain), (index % PerTerrain) + 1)
            : throw new ArgumentOutOfRangeException(nameof(index), index, "Location index must be 0..23.");

    /// <summary>All 24 locations in index order.</summary>
    public static IReadOnlyList<LocationId> All { get; } =
        [.. Enumerable.Range(0, TotalCount).Select(FromIndex)];

    /// <summary>Parses <c>B1</c>/<c>F6</c>/<c>M8</c>. Also accepts the books' reversed form (<c>1B</c>).</summary>
    public static LocationId Parse(string text)
    {
        if (!TryParse(text, out LocationId location))
        {
            throw new FormatException($"'{text}' is not a location (expected e.g. B1, F6, M8).");
        }

        return location;
    }

    public static bool TryParse([NotNullWhen(true)] string? text, out LocationId location)
    {
        location = default;
        if (text is null)
        {
            return false;
        }

        ReadOnlySpan<char> span = text.AsSpan().Trim();
        if (span.Length != 2)
        {
            return false;
        }

        // Accept both "B1" and the scenario books' "1B".
        (char letter, char digit) = char.IsAsciiDigit(span[0]) ? (span[1], span[0]) : (span[0], span[1]);
        if (!char.IsAsciiDigit(digit) || letter is not ('B' or 'F' or 'M' or 'b' or 'f' or 'm'))
        {
            return false;
        }

        int number = digit - '0';
        if (number is < 1 or > PerTerrain)
        {
            return false;
        }

        location = new LocationId(Terrains.FromCode(letter), number);
        return true;
    }

    public override string ToString() => $"{Terrain.ToCode()}{Number}";
}
