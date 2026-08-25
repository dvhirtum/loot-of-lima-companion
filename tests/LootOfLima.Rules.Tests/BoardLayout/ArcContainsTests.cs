namespace LootOfLima.Rules.Tests;

public class ArcContainsTests
{
    [Fact]
    public void Should_contain_the_start_sector_when_the_arrows_are_adjacent()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction direction in Directions.All)
        {
            // Act
            bool actual = layout.ArcContains(direction, direction.Next(), direction);

            // Assert
            Assert.True(actual);
        }
    }

    [Fact]
    public void Should_include_the_start_but_exclude_the_end_when_the_arc_is_tested()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        // Act
        bool start = layout.ArcContains(Direction.N, Direction.E, Direction.N);
        bool between = layout.ArcContains(Direction.N, Direction.E, Direction.NE);
        bool end = layout.ArcContains(Direction.N, Direction.E, Direction.E);

        // Assert
        Assert.True(start);
        Assert.True(between);
        Assert.False(end);
    }

    [Fact]
    public void Should_include_the_sectors_either_side_of_north_when_the_arc_wraps_past_it()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        // Act
        bool northWest = layout.ArcContains(Direction.W, Direction.NE, Direction.NW);
        bool north = layout.ArcContains(Direction.W, Direction.NE, Direction.N);
        bool northEast = layout.ArcContains(Direction.W, Direction.NE, Direction.NE);

        // Assert
        Assert.True(northWest);
        Assert.True(north);
        Assert.False(northEast);
    }
}
