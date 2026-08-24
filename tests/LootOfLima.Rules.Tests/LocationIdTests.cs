namespace LootOfLima.Rules.Tests;

public class LocationIdTests
{
    [Fact]
    public void Index_round_trips_for_all_locations()
    {
        for (var index = 0; index < LocationId.TotalCount; index++)
        {
            Assert.Equal(index, LocationId.FromIndex(index).Index);
        }
    }

    [Theory]
    [InlineData("B1", Terrain.Beach, 1)]
    [InlineData("F6", Terrain.Forest, 6)]
    [InlineData("M8", Terrain.Mountain, 8)]
    [InlineData("3M", Terrain.Mountain, 3)]
    [InlineData("1b", Terrain.Beach, 1)]
    public void Parses_both_notations(string text, Terrain terrain, int number)
    {
        Assert.Equal(new LocationId(terrain, number), LocationId.Parse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("B")]
    [InlineData("B0")]
    [InlineData("B9")]
    [InlineData("X1")]
    [InlineData("B12")]
    public void Rejects_malformed_input(string text)
    {
        Assert.False(LocationId.TryParse(text, out _));
    }

    [Fact]
    public void ToString_uses_the_canonical_notation()
    {
        Assert.Equal("M3", new LocationId(Terrain.Mountain, 3).ToString());
    }
}
