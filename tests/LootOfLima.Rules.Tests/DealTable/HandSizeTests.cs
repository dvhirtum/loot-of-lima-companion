using Sut = LootOfLima.Rules.DealTable;

namespace LootOfLima.Rules.Tests.DealTable;

public class HandSizeTests
{
    [Theory]
    [InlineData(3, 7)]
    [InlineData(4, 5)]
    [InlineData(5, 4)]
    public void Should_return_the_printed_hand_size_when_the_table_is_this_size(int playerCount, int expected)
    {
        // Arrange
        // Act
        int actual = Sut.HandSize(playerCount);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Should_throw_when_the_table_is_the_wrong_size(int playerCount)
    {
        // Arrange
        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => Sut.HandSize(playerCount));

        // Assert
        Assert.Equal("playerCount", exception.ParamName);
    }
}
