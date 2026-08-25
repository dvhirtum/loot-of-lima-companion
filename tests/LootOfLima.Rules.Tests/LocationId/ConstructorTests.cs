using Sut = LootOfLima.Rules.LocationId;

namespace LootOfLima.Rules.Tests.LocationId;

public class ConstructorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void Should_throw_when_the_number_is_outside_1_to_8(int number)
    {
        // Arrange
        // (nothing to set up - the constructor is the subject)

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sut(Terrain.Beach, number));

        // Assert
        Assert.Equal("Number", exception.ParamName);
    }

    [Fact]
    public void Should_accept_every_number_when_it_is_1_to_8()
    {
        // Arrange
        // (nothing to set up - the constructor is the subject)

        for (int number = 1; number <= Sut.PerTerrain; number++)
        {
            // Act
            Sut location = new(Terrain.Forest, number);

            // Assert
            Assert.Equal(number, location.Number);
        }
    }
}
