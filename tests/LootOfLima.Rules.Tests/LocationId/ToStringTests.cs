using Sut = LootOfLima.Rules.LocationId;

namespace LootOfLima.Rules.Tests.LocationId;

public class ToStringTests
{
    [Fact]
    public void Should_return_the_canonical_notation_when_a_location_is_formatted()
    {
        // Arrange
        Sut location = new(Terrain.Mountain, 3);

        // Act
        string actual = location.ToString();

        // Assert
        Assert.Equal("M3", actual);
    }
}
