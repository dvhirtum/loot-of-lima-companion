using LootOfLima.Rules;

namespace LootOfLima.Scenarios.Tests;

/// <summary>Structural checks on the decoded seed data, independent of the oracle.</summary>
public class ScenarioSeedIntegrityTests
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
    public void Deal_partitions_all_24_locations(string id)
    {
        var scenario = ScenarioLibrary.Get(id);

        Assert.Equal(5, scenario.Hands.Count);
        Assert.All(scenario.Hands, hand => Assert.Equal(4, hand.Count));
        Assert.Equal(2, scenario.PublicReveal.Count);
        Assert.Equal(2, scenario.Treasure.Count);

        var everything = scenario.Hands
            .SelectMany(hand => hand)
            .Concat(scenario.PublicReveal)
            .Concat(scenario.Treasure)
            .ToList();

        Assert.Equal(LocationId.TotalCount, everything.Count);
        Assert.Equal(LocationId.TotalCount, everything.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Treasure_is_exactly_what_the_deal_leaves_over(string id)
    {
        var scenario = ScenarioLibrary.Get(id);

        var dealt = scenario.Hands.SelectMany(hand => hand).Concat(scenario.PublicReveal);
        var leftOver = LocationId.All.Except(dealt).Order().ToArray();

        Assert.Equal(scenario.Treasure.Order().ToArray(), leftOver);
    }

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Printed_fewest_terrain_really_is_a_minimum(string id)
    {
        var scenario = ScenarioLibrary.Get(id);

        for (var seat = 0; seat < scenario.Hands.Count; seat++)
        {
            if (scenario.FewestTerrain[seat] is not { } fewest)
            {
                continue;
            }

            var counts = Terrains.All
                .Select(terrain => scenario.Hands[seat].Count(l => l.Terrain == terrain))
                .ToArray();

            var actual = scenario.Hands[seat].Count(l => l.Terrain == fewest);
            Assert.Equal(counts.Min(), actual);
        }
    }

    [Fact]
    public void Every_scenario_has_a_distinct_id_and_a_full_answer_block()
    {
        Assert.Equal(ScenarioLibrary.Ids.Count, ScenarioLibrary.Ids.Distinct().Count());

        foreach (var id in ScenarioLibrary.Ids)
        {
            Assert.Equal(ScenarioLibrary.AnswersPerScenario, ScenarioLibrary.PublishedAnswers(id).Length);
        }
    }

    [Fact]
    public void Scenarios_span_all_twenty_books()
    {
        var books = ScenarioLibrary.All.Select(s => s.Book).Distinct().Order().ToArray();
        Assert.Equal(Enumerable.Range(1, 20).ToArray(), books);
    }
}
