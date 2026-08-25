namespace LootOfLima.Rules.Tests;

public class SectorOfTests
{
    [Fact]
    public void Should_place_exactly_three_locations_in_every_sector_when_all_24_are_mapped()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;
        int[] perSector = new int[Directions.Count];

        // Act
        foreach (LocationId location in LocationId.All)
        {
            perSector[(int)layout.SectorOf(location)]++;
        }

        // Assert
        Assert.Equal(24, LocationId.All.Count);
        Assert.All(perSector, count => Assert.Equal(3, count));
    }
}
