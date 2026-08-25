using LootOfLima.Rules;
using Library = LootOfLima.Scenarios.ScenarioLibrary;
using Sut = LootOfLima.Scenarios.Scenario;

namespace LootOfLima.Scenarios.Tests.Scenario;

public class HandsTests
{
    [Theory]
    [MemberData(nameof(ScenarioIds.All), MemberType = typeof(ScenarioIds))]
    public void Should_partition_all_24_locations_when_the_deal_is_reassembled(string id)
    {
        // Arrange
        Sut scenario = Library.Get(id);

        // Act
        List<LocationId> everything =
        [
            .. scenario.Hands.SelectMany(hand => hand),
            .. scenario.PublicReveal,
            .. scenario.Treasure,
        ];

        // Assert
        Assert.Equal(5, scenario.Hands.Count);
        Assert.All(scenario.Hands, hand => Assert.Equal(4, hand.Count));
        Assert.Equal(2, scenario.PublicReveal.Count);
        Assert.Equal(2, scenario.Treasure.Count);
        Assert.Equal(LocationId.TotalCount, everything.Count);
        Assert.Equal(LocationId.TotalCount, everything.Distinct().Count());
    }
}
