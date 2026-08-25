namespace LootOfLima.Rules.Tests;

public class EngineTests
{
    private static readonly BoardLayout Layout = BoardLayout.Default;

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
    public void Reproduces_the_published_mountain_answers(Direction start, Direction end, int expected)
    {
        QueryRange range = new(start, end, Terrain.Mountain);
        Assert.Equal(expected, Engine.CountInRange(Book1Scenario1Player3, range, Layout));
    }

    [Theory]
    [InlineData(Direction.N, Direction.N, 1)]
    [InlineData(Direction.N, Direction.NE, 0)]
    [InlineData(Direction.N, Direction.SW, 0)]
    [InlineData(Direction.N, Direction.W, 0)]
    [InlineData(Direction.N, Direction.NW, 1)]
    public void Reproduces_the_published_beach_answers(Direction start, Direction end, int expected)
    {
        QueryRange range = new(start, end, Terrain.Beach);
        Assert.Equal(expected, Engine.CountInRange(Book1Scenario1Player3, range, Layout));
    }

    [Fact]
    public void Player_with_no_forest_tokens_answers_zero_to_every_forest_question()
    {
        foreach (Direction start in Directions.All)
        {
            foreach (Direction end in Directions.All)
            {
                QueryRange range = new(start, end, Terrain.Forest);
                Assert.Equal(0, Engine.CountInRange(Book1Scenario1Player3, range, Layout));
            }
        }
    }

    [Fact]
    public void All_terrain_answer_equals_the_sum_of_the_three_terrain_answers()
    {
        foreach (Direction start in Directions.All)
        {
            foreach (Direction end in Directions.All)
            {
                int all = Engine.CountInRange(Book1Scenario1Player3, new QueryRange(start, end), Layout);
                int summed = Terrains.All.Sum(terrain =>
                    Engine.CountInRange(Book1Scenario1Player3, new QueryRange(start, end, terrain), Layout));

                Assert.Equal(all, summed);
            }
        }
    }

    [Fact]
    public void Coincident_arrows_return_the_whole_hand()
    {
        foreach (Direction direction in Directions.All)
        {
            QueryRange range = new(direction, direction);
            Assert.Equal(
                Book1Scenario1Player3.Length,
                Engine.CountInRange(Book1Scenario1Player3, range, Layout));
        }
    }

    [Fact]
    public void Complementary_arcs_partition_the_hand()
    {
        foreach (Direction a in Directions.All)
        {
            foreach (Direction b in Directions.All)
            {
                if (a == b)
                {
                    continue;
                }

                int forward = Engine.CountInRange(Book1Scenario1Player3, new QueryRange(a, b), Layout);
                int backward = Engine.CountInRange(Book1Scenario1Player3, new QueryRange(b, a), Layout);

                Assert.Equal(Book1Scenario1Player3.Length, forward + backward);
            }
        }
    }

    [Fact]
    public void Empty_hand_always_answers_zero() =>
        Assert.Equal(0, Engine.CountInRange([], new QueryRange(Direction.N, Direction.S), Layout));
}
