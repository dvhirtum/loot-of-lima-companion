using Sut = LootOfLima.Rules.LocationId;

namespace LootOfLima.Rules.Tests.LocationId;

public class FromIndexTests
{
    [Fact]
    public void Should_round_trip_the_index_when_every_location_is_rebuilt_from_it()
    {
        // Arrange
        // (nothing to set up — LocationId.FromIndex is a pure static factory)

        for (int index = 0; index < Sut.TotalCount; index++)
        {
            // Act
            int actual = Sut.FromIndex(index).Index;

            // Assert
            Assert.Equal(index, actual);
        }
    }
}
