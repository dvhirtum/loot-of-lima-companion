using LootOfLima.Rules;
using Library = LootOfLima.Scenarios.ScenarioLibrary;
using Sut = LootOfLima.Scenarios.Scenario;

namespace LootOfLima.Scenarios.Tests.Scenario;

public class TreasureTests
{
    [Theory]
    [MemberData(nameof(ScenarioIds.All), MemberType = typeof(ScenarioIds))]
    public void Should_be_exactly_what_the_deal_leaves_over_when_the_hands_are_subtracted(string id)
    {
        // Arrange
        Sut scenario = Library.Get(id);
        IEnumerable<LocationId> dealt = scenario.Hands.SelectMany(hand => hand).Concat(scenario.PublicReveal);

        // Act
        LocationId[] leftOver = LocationId.All.Except(dealt).Order().ToArray();

        // Assert
        Assert.Equal(scenario.Treasure.Order().ToArray(), leftOver);
    }
}
