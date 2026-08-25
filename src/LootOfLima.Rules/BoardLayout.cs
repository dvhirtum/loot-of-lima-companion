using System.Reflection;
using System.Text.Json;

namespace LootOfLima.Rules;

/// <summary>
/// Which of the 24 locations sits in which compass sector, plus the precomputed arc masks.
/// </summary>
/// <remarks>
/// This is data, never logic. The layout was recovered by fitting candidate models against the
/// official solo scenario books and is guarded by the scenario oracle test, so editing
/// <c>Data/board-layout.json</c> incorrectly fails the build's test run immediately.
/// </remarks>
public sealed class BoardLayout
{
    private const string ResourceName = "LootOfLima.Rules.Data.board-layout.json";

    /// <summary>Sector index (0..7) for each location index (0..23).</summary>
    private readonly byte[] _sectorOfLocation;

    /// <summary>For each (start, end) pair, a bitmask over the 8 sectors covered by the arc.</summary>
    private readonly byte[] _arcSectorMasks;

    private BoardLayout(byte[] sectorOfLocation, byte[] arcSectorMasks)
    {
        _sectorOfLocation = sectorOfLocation;
        _arcSectorMasks = arcSectorMasks;
    }

    private static readonly Lazy<BoardLayout> LazyDefault = new(LoadEmbedded, isThreadSafe: true);

    /// <summary>The shipped island layout.</summary>
    public static BoardLayout Default => LazyDefault.Value;

    public Direction SectorOf(LocationId location)
    {
        // default(LocationId) has Index -1, which would read out of bounds. The constructor cannot
        // reject it, so the guard has to live here, where the index is first used.
        if (!location.IsValid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(location), location, "Location is not one of the 24 island locations.");
        }

        return (Direction)_sectorOfLocation[location.Index];
    }

    /// <summary>The locations in a given sector, in index order.</summary>
    public IReadOnlyList<LocationId> LocationsIn(Direction sector) =>
        [.. LocationId.All.Where(l => SectorOf(l) == sector)];

    /// <summary>Bitmask over the 8 sectors covered by the clockwise arc <c>[start, end)</c>.</summary>
    public byte ArcSectorMask(Direction start, Direction end) =>
        _arcSectorMasks[((int)start * Directions.Count) + (int)end];

    /// <summary>How many sectors the arc covers (1..8).</summary>
    public int ArcSectorCount(Direction start, Direction end) =>
        System.Numerics.BitOperations.PopCount(ArcSectorMask(start, end));

    public bool ArcContains(Direction start, Direction end, Direction sector) =>
        (ArcSectorMask(start, end) & (1 << (int)sector)) != 0;

    private static BoardLayout LoadEmbedded()
    {
        using Stream stream = typeof(BoardLayout).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        return Load(stream);
    }

    public static BoardLayout Load(Stream json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Load(document.RootElement);
    }

    public static BoardLayout Load(JsonElement root)
    {
        JsonElement semantics = root.GetProperty("arcSemantics");
        Require(semantics.GetProperty("startInclusive").GetBoolean(), "startInclusive must be true.");
        Require(semantics.GetProperty("endExclusive").GetBoolean(), "endExclusive must be true.");
        Require(
            semantics.GetProperty("startEqualsEndMeansFullCircle").GetBoolean(),
            "startEqualsEndMeansFullCircle must be true.");

        byte[] sectorOfLocation = new byte[LocationId.TotalCount];
        bool[] assigned = new bool[LocationId.TotalCount];

        JsonElement sectors = root.GetProperty("sectors");
        Require(
            sectors.GetArrayLength() == Directions.Count,
            $"Expected {Directions.Count} sectors, found {sectors.GetArrayLength()}.");

        bool[] seenSectors = new bool[Directions.Count];
        foreach (JsonElement sector in sectors.EnumerateArray())
        {
            Direction direction = Directions.Parse(sector.GetProperty("direction").GetString() ?? string.Empty);
            Require(!seenSectors[(int)direction], $"Sector {direction} appears more than once.");
            seenSectors[(int)direction] = true;

            JsonElement locations = sector.GetProperty("locations");
            Require(
                locations.GetArrayLength() == 3,
                $"Sector {direction} must hold exactly 3 locations, found {locations.GetArrayLength()}.");

            foreach (JsonElement element in locations.EnumerateArray())
            {
                LocationId location = LocationId.Parse(element.GetString() ?? string.Empty);
                Require(!assigned[location.Index], $"Location {location} is assigned to more than one sector.");
                assigned[location.Index] = true;
                sectorOfLocation[location.Index] = (byte)direction;
            }
        }

        for (int i = 0; i < LocationId.TotalCount; i++)
        {
            Require(assigned[i], $"Location {LocationId.FromIndex(i)} is not assigned to any sector.");
        }

        return new BoardLayout(sectorOfLocation, BuildArcMasks());
    }

    /// <summary>
    /// Half-open clockwise arcs: include <c>start</c>, walk clockwise, stop before <c>end</c>.
    /// Coincident arrows mean the whole circle rather than an empty arc.
    /// </summary>
    private static byte[] BuildArcMasks()
    {
        byte[] masks = new byte[Directions.Count * Directions.Count];
        for (int start = 0; start < Directions.Count; start++)
        {
            for (int end = 0; end < Directions.Count; end++)
            {
                int span = start == end ? Directions.Count : ((end - start + Directions.Count) % Directions.Count);
                int mask = 0;
                for (int step = 0; step < span; step++)
                {
                    mask |= 1 << ((start + step) % Directions.Count);
                }

                masks[(start * Directions.Count) + end] = (byte)mask;
            }
        }

        return masks;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Invalid board layout: {message}");
        }
    }
}
