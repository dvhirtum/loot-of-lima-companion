using Sut = LootOfLima.Rules.BoardLayout;

namespace LootOfLima.Rules.Tests.BoardLayout;

public class ArcSectorCountTests
{
    [Fact]
    public void Should_count_all_eight_sectors_when_both_arrows_point_the_same_way()
    {
        // Arrange
        Sut layout = Sut.Default;

        foreach (Direction direction in Directions.All)
        {
            // Act
            int actual = layout.ArcSectorCount(direction, direction);

            // Assert
            Assert.Equal(Directions.Count, actual);
        }
    }

    [Fact]
    public void Should_count_only_the_start_sector_when_the_arrows_are_adjacent()
    {
        // Arrange
        Sut layout = Sut.Default;

        foreach (Direction direction in Directions.All)
        {
            // Act
            int actual = layout.ArcSectorCount(direction, direction.Next());

            // Assert
            Assert.Equal(1, actual);
        }
    }

    [Fact]
    public void Should_count_three_sectors_when_the_arc_wraps_past_north()
    {
        // Arrange
        Sut layout = Sut.Default;

        // Act
        int actual = layout.ArcSectorCount(Direction.W, Direction.NE);

        // Assert
        Assert.Equal(3, actual);
    }
}
