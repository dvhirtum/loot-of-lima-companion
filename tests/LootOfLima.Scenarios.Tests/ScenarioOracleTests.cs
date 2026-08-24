using LootOfLima.Rules;

namespace LootOfLima.Scenarios.Tests;

/// <summary>
/// The oracle. Every answer printed in the official scenario books must be reproduced exactly by
/// the engine. One test simultaneously validates the board layout, the half-open arc semantics,
/// <see cref="Engine.CountInRange"/> and the decoded seed data.
/// </summary>
public class ScenarioOracleTests
{
    public static TheoryData<string> AllScenarioIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var id in ScenarioLibrary.Ids)
            {
                data.Add(id);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Engine_reproduces_every_published_answer(string id)
    {
        var scenario = ScenarioLibrary.Get(id);
        var published = ScenarioLibrary.PublishedAnswers(id).Span;
        var layout = BoardLayout.Default;

        foreach (var start in Directions.All)
        {
            foreach (var end in Directions.All)
            {
                for (var terrainRow = 0; terrainRow < ScenarioLibrary.TerrainRows.Count; terrainRow++)
                {
                    var range = new QueryRange(start, end, ScenarioLibrary.TerrainRows[terrainRow]);

                    for (var player = ScenarioLibrary.FirstAnsweringPlayer;
                         player <= ScenarioLibrary.LastAnsweringPlayer;
                         player++)
                    {
                        var expected = published[ScenarioLibrary.AnswerIndex(start, end, terrainRow, player)];
                        var actual = Engine.CountInRange(scenario.Hands[player - 1], range, layout);

                        Assert.Equal(expected, actual);
                    }
                }
            }
        }
    }

    [Fact]
    public void Library_holds_the_expected_number_of_scenarios()
    {
        // 240 are printed across the 20 books. Book 16 scenario 12 is dropped: its entry on the
        // Scenario Tokens Chart is a duplicate of scenario 11's, which contradicts its own
        // question table. That is an error in the printed book, not in the importer.
        Assert.Equal(239, ScenarioLibrary.All.Count);
        Assert.DoesNotContain("b16s12", ScenarioLibrary.Ids);
    }
}
