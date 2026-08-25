using Location = LootOfLima.Rules.LocationId;
using Sut = LootOfLima.Rules.BoardLayout;

namespace LootOfLima.Rules.Tests.BoardLayout;

public class SectorOfTests
{
    [Fact]
    public void Should_place_exactly_three_locations_in_every_sector_when_all_24_are_mapped()
    {
        // Arrange
        Sut layout = Sut.Default;
        int[] perSector = new int[Directions.Count];

        // Act
        foreach (Location location in Location.All)
        {
            perSector[(int)layout.SectorOf(location)]++;
        }

        // Assert
        Assert.Equal(24, Location.All.Count);
        Assert.All(perSector, count => Assert.Equal(3, count));
    }
}
