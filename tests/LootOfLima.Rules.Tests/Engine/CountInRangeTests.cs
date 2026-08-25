namespace LootOfLima.Rules.Tests;

public class CountInRangeTests
{
    /// <summary>
    /// Book 1, Scenario 1, Player 3, decoded from the official scenario book and cross-checked
    /// against that same book's published answers.
    /// </summary>
    private static readonly LocationId[] Book1Scenario1Player3 =
    [
        new(Terrain.Mountain, 1),
        new(Terrain.Mountain, 2),
        new(Terrain.Mountain, 5),
        new(Terrain.Beach, 7),
    ];

    [Theory]
    [InlineData(Direction.N, Direction.N, 3)]
    [InlineData(Direction.N, Direction.NE, 1)]
    [InlineData(Direction.N, Direction.E, 2)]
    [InlineData(Direction.N, Direction.SE, 2)]
    [InlineData(Direction.N, Direction.S, 2)]
    [InlineData(Direction.N, Direction.SW, 3)]
    public void Should_return_the_published_answer_when_the_question_is_restricted_to_mountain(
        Direction start,
        Direction end,
        int expected)
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;
        QueryRange range = new(start, end, Terrain.Mountain);

        // Act
        int actual = Engine.CountInRange(Book1Scenario1Player3, range, layout);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(Direction.N, Direction.N, 1)]
    [InlineData(Direction.N, Direction.NE, 0)]
    [InlineData(Direction.N, Direction.SW, 0)]
    [InlineData(Direction.N, Direction.W, 0)]
    [InlineData(Direction.N, Direction.NW, 1)]
    public void Should_return_the_published_answer_when_the_question_is_restricted_to_beach(
        Direction start,
        Direction end,
        int expected)
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;
        QueryRange range = new(start, end, Terrain.Beach);

        // Act
        int actual = Engine.CountInRange(Book1Scenario1Player3, range, layout);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Should_return_zero_when_the_hand_holds_no_token_of_the_asked_terrain()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction start in Directions.All)
        {
            foreach (Direction end in Directions.All)
            {
                QueryRange range = new(start, end, Terrain.Forest);

                // Act
                int actual = Engine.CountInRange(Book1Scenario1Player3, range, layout);

                // Assert
                Assert.Equal(0, actual);
            }
        }
    }

    [Fact]
    public void Should_equal_the_sum_of_the_three_terrain_answers_when_no_terrain_filter_is_set()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction start in Directions.All)
        {
            foreach (Direction end in Directions.All)
            {
                // Act
                int unfiltered = Engine.CountInRange(Book1Scenario1Player3, new QueryRange(start, end), layout);
                int summed = Terrains.All.Sum(terrain =>
                    Engine.CountInRange(Book1Scenario1Player3, new QueryRange(start, end, terrain), layout));

                // Assert
                Assert.Equal(unfiltered, summed);
            }
        }
    }

    [Fact]
    public void Should_return_the_whole_hand_when_both_arrows_point_the_same_way()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction direction in Directions.All)
        {
            QueryRange range = new(direction, direction);

            // Act
            int actual = Engine.CountInRange(Book1Scenario1Player3, range, layout);

            // Assert
            Assert.Equal(Book1Scenario1Player3.Length, actual);
        }
    }

    [Fact]
    public void Should_partition_the_hand_when_an_arc_and_its_reverse_are_counted()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction a in Directions.All)
        {
            foreach (Direction b in Directions.All)
            {
                if (a == b)
                {
                    continue;
                }

                // Act
                int forward = Engine.CountInRange(Book1Scenario1Player3, new QueryRange(a, b), layout);
                int backward = Engine.CountInRange(Book1Scenario1Player3, new QueryRange(b, a), layout);

                // Assert
                Assert.Equal(Book1Scenario1Player3.Length, forward + backward);
            }
        }
    }

    [Fact]
    public void Should_return_zero_when_the_hand_is_empty()
    {
        // Arrange
        BoardLayout layout = BoardLayout.Default;
        QueryRange range = new(Direction.N, Direction.S);

        // Act
        int actual = Engine.CountInRange([], range, layout);

        // Assert
        Assert.Equal(0, actual);
    }
}
