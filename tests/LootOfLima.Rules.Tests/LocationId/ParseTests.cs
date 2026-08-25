using Sut = LootOfLima.Rules.LocationId;

namespace LootOfLima.Rules.Tests.LocationId;

public class ParseTests
{
    [Theory]
    [InlineData("B1", Terrain.Beach, 1)]
    [InlineData("F6", Terrain.Forest, 6)]
    [InlineData("M8", Terrain.Mountain, 8)]
    [InlineData("3M", Terrain.Mountain, 3)]
    [InlineData("1b", Terrain.Beach, 1)]
    public void Should_return_the_location_when_either_notation_is_given(string text, Terrain terrain, int number)
    {
        // Arrange
        Sut expected = new(terrain, number);

        // Act
        Sut actual = Sut.Parse(text);

        // Assert
        Assert.Equal(expected, actual);
    }
}
