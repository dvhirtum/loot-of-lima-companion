using LootOfLima.Rules;
using Deal = LootOfLima.Scenarios.Scenario;
using Library = LootOfLima.Scenarios.ScenarioLibrary;
using Sut = LootOfLima.Rules.Engine;

namespace LootOfLima.Scenarios.Tests.Engine;

/// <summary>
/// The oracle. Every answer printed in the official scenario books must be reproduced exactly by
/// the engine. One test simultaneously validates the board layout, the half-open arc semantics,
/// <see cref="Sut.CountInRange"/> and the decoded seed data.
/// </summary>
public class CountInRangeOracleTests
{
    [Theory]
    [MemberData(nameof(ScenarioIds.All), MemberType = typeof(ScenarioIds))]
    public void Should_reproduce_every_published_answer_when_a_scenario_is_replayed(string id)
    {
        // Arrange
        Deal scenario = Library.Get(id);
        ReadOnlySpan<byte> published = Library.PublishedAnswers(id).Span;
        BoardLayout layout = BoardLayout.Default;

        foreach (Direction start in Directions.All)
        {
            foreach (Direction end in Directions.All)
            {
                for (int terrainRow = 0; terrainRow < Library.TerrainRows.Count; terrainRow++)
                {
                    QueryRange range = new(start, end, Library.TerrainRows[terrainRow]);

                    for (int player = Library.FirstAnsweringPlayer;
                         player <= Library.LastAnsweringPlayer;
                         player++)
                    {
                        // Act
                        int actual = Sut.CountInRange(scenario.Hands[player - 1], range, layout);

                        // Assert
                        byte expected = published[Library.AnswerIndex(start, end, terrainRow, player)];
                        Assert.Equal(expected, actual);
                    }
                }
            }
        }
    }
}
