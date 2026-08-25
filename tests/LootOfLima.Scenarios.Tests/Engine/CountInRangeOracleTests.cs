using LootOfLima.Rules;

namespace LootOfLima.Scenarios.Tests;

/// <summary>
/// The oracle. Every answer printed in the official scenario books must be reproduced exactly by
/// the engine. One test simultaneously validates the board layout, the half-open arc semantics,
/// <see cref="Engine.CountInRange"/> and the decoded seed data.
/// </summary>
public class CountInRangeOracleTests
{
    [Theory]
    [MemberData(nameof(ScenarioIds.All), MemberType = typeof(ScenarioIds))]
    public void Should_reproduce_every_published_answer_when_a_scenario_is_replayed(string id)
    {
        // Arrange
        Scenario scenario = ScenarioLibrary.Get(id);
        ReadOnlySpan<byte> published = ScenarioLibrary.PublishedAnswers(id).Span;
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction start in Directions.All)
        {
            foreach (Direction end in Directions.All)
            {
                for (int terrainRow = 0; terrainRow < ScenarioLibrary.TerrainRows.Count; terrainRow++)
                {
                    QueryRange range = new(start, end, ScenarioLibrary.TerrainRows[terrainRow]);

                    for (int player = ScenarioLibrary.FirstAnsweringPlayer;
                         player <= ScenarioLibrary.LastAnsweringPlayer;
                         player++)
                    {
                        // Act
                        int actual = Engine.CountInRange(scenario.Hands[player - 1], range, layout);

                        // Assert
                        byte expected = published[ScenarioLibrary.AnswerIndex(start, end, terrainRow, player)];
                        Assert.Equal(expected, actual);
                    }
                }
            }
        }
    }
}
