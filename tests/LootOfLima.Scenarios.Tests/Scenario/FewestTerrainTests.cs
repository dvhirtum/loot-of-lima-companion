using LootOfLima.Rules;

namespace LootOfLima.Scenarios.Tests;

public class FewestTerrainTests
{
    [Theory]
    [MemberData(nameof(ScenarioIds.All), MemberType = typeof(ScenarioIds))]
    public void Should_name_a_real_minimum_when_a_seat_has_a_printed_fewest_terrain(string id)
    {
        // Arrange
        Scenario scenario = ScenarioLibrary.Get(id);

        for (int seat = 0; seat < scenario.Hands.Count; seat++)
        {
            if (scenario.FewestTerrain[seat] is not { } fewest)
            {
                continue;
            }

            // Act
            int actual = scenario.Hands[seat].Count(l => l.Terrain == fewest);

            // Assert
            int[] counts = [.. Terrains.All.Select(t => scenario.Hands[seat].Count(l => l.Terrain == t))];
            Assert.Equal(counts.Min(), actual);
        }
    }
}
