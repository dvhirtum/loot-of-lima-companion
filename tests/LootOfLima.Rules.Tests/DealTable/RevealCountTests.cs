using Location = LootOfLima.Rules.LocationId;
using Sut = LootOfLima.Rules.DealTable;

namespace LootOfLima.Rules.Tests.DealTable;

public class RevealCountTests
{
    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 2)]
    public void Should_return_the_printed_reveal_count_when_the_table_is_this_size(int playerCount, int expected)
    {
        // Arrange
        // Act
        int actual = Sut.RevealCount(playerCount);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_account_for_all_24_locations_when_the_setup_is_added_up(int playerCount)
    {
        // Arrange
        // Act
        int total = (playerCount * Sut.HandSize(playerCount)) + Sut.RevealCount(playerCount) + Sut.TreasureSize;

        // Assert
        Assert.Equal(Location.TotalCount, total);
    }
}
