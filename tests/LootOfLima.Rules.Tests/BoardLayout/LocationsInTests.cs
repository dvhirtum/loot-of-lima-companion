namespace LootOfLima.Rules.Tests;

public class LocationsInTests
{
    [Fact]
    public void Should_return_one_location_of_each_terrain_when_any_sector_is_asked_for()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction sector in Directions.All)
        {
            // Act
            Terrain[] terrains = layout.LocationsIn(sector).Select(l => l.Terrain).Order().ToArray();

            // Assert
            Assert.Equal([Terrain.Beach, Terrain.Forest, Terrain.Mountain], terrains);
        }
    }
}
