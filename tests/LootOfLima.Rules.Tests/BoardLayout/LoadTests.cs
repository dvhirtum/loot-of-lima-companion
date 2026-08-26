using System.Text.Json;
using System.Text.Json.Nodes;
using Location = LootOfLima.Rules.LocationId;
using Sut = LootOfLima.Rules.BoardLayout;

namespace LootOfLima.Rules.Tests.BoardLayout;

public class LoadTests
{
    [Fact]
    public void Should_load_the_shipped_layout_when_it_is_read_unchanged()
    {
        // Arrange
        JsonNode layout = ShippedLayout();

        // Act
        Sut loaded = Load(layout);

        // Assert
        Assert.Equal(24, Location.All.Count);
        Assert.All(Location.All, location => Assert.Contains(loaded.SectorOf(location), Directions.All));
    }

    [Fact]
    public void Should_throw_when_the_sector_order_disagrees_with_the_clockwise_ring()
    {
        // Arrange
        // The trap this closes: the array reads like the source of the ring, but the ring is the
        // Direction enum. Reordered, every arc used to keep its old meaning and nothing complained.
        JsonNode layout = ShippedLayout();
        JsonArray order = layout["sectorOrder"]!.AsArray();
        (order[1], order[2]) = (order[2]!.DeepClone(), order[1]!.DeepClone());

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load(layout));

        // Assert
        Assert.Contains("sectorOrder[1]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_throw_when_the_sector_order_is_not_the_whole_compass()
    {
        // Arrange
        JsonNode layout = ShippedLayout();
        layout["sectorOrder"]!.AsArray().RemoveAt(7);

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Load(layout));

        // Assert
        Assert.Contains("sectorOrder lists 7 directions", exception.Message, StringComparison.Ordinal);
    }

    private static Sut Load(JsonNode layout)
    {
        using JsonDocument document = JsonDocument.Parse(layout.ToJsonString());
        return Sut.Load(document.RootElement);
    }

    /// <summary>The layout the assembly actually ships, so a test mutates the real thing.</summary>
    private static JsonNode ShippedLayout()
    {
        string resource = typeof(Sut).Assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("board-layout.json", StringComparison.Ordinal));

        using Stream stream = typeof(Sut).Assembly.GetManifestResourceStream(resource)!;
        return JsonNode.Parse(stream)!;
    }
}
