using Deals = LootOfLima.Rules.Dealer;
using Location = LootOfLima.Rules.LocationId;
using Setup = LootOfLima.Rules.Deal;
using Sut = LootOfLima.Rules.FewestTerrainRule;

namespace LootOfLima.Rules.Tests.FewestTerrainRule;

public class MinimaTests
{
    private static IReadOnlyList<Location> Hand(params string[] locations) => [.. locations.Select(Location.Parse)];

    [Fact]
    public void Should_name_one_terrain_when_a_single_terrain_is_rarest()
    {
        // Arrange
        // 2 beach, 1 forest, 3 mountain.
        IReadOnlyList<Location> hand = Hand("B1", "B2", "F3", "M4", "M5", "M6");

        // Act
        IReadOnlyList<Terrain> minima = Sut.Minima(hand);

        // Assert
        Assert.Equal([Terrain.Forest], minima);
    }

    [Fact]
    public void Should_name_a_terrain_the_hand_has_none_of_when_the_hand_is_missing_one()
    {
        // Arrange
        // "If a player does not have any tokens of a terrain, then that is the terrain they have
        // the least of." 2 beach, 2 forest, no mountain.
        IReadOnlyList<Location> hand = Hand("B1", "B2", "F3", "F4");

        // Act
        IReadOnlyList<Terrain> minima = Sut.Minima(hand);

        // Assert
        Assert.Equal([Terrain.Mountain], minima);
    }

    [Fact]
    public void Should_name_both_terrains_when_two_tie_for_fewest()
    {
        // Arrange
        // The rulebook's tie case: 2 beach, 1 forest, 1 mountain. The player picks one of the two,
        // so the rule can only say which are eligible.
        IReadOnlyList<Location> hand = Hand("B1", "B2", "F3", "M4");

        // Act
        IReadOnlyList<Terrain> minima = Sut.Minima(hand);

        // Assert
        Assert.Equal([Terrain.Forest, Terrain.Mountain], minima);
    }

    [Fact]
    public void Should_name_every_terrain_when_the_hand_is_evenly_split()
    {
        // Arrange
        IReadOnlyList<Location> hand = Hand("B1", "F2", "M3");

        // Act
        IReadOnlyList<Terrain> minima = Sut.Minima(hand);

        // Assert
        Assert.Equal(Terrains.All, minima);
    }

    [Fact]
    public void Should_name_every_terrain_when_the_hand_is_empty()
    {
        // Arrange
        // Holding none of a terrain counts as fewest, so an empty hand ties all three at zero.
        IReadOnlyList<Location> hand = [];

        // Act
        IReadOnlyList<Terrain> minima = Sut.Minima(hand);

        // Assert
        Assert.Equal(Terrains.All, minima);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_name_a_terrain_that_really_is_rarest_when_a_real_hand_is_dealt(int playerCount)
    {
        // Arrange
        Setup deal = Deals.Create(playerCount, TestGame.DefaultSeed);

        foreach (IReadOnlyList<Location> hand in deal.Hands)
        {
            // Act
            IReadOnlyList<Terrain> minima = Sut.Minima(hand);

            // Assert
            int fewest = Terrains.All.Min(terrain => hand.Count(location => location.Terrain == terrain));
            Assert.NotEmpty(minima);
            Assert.All(minima, terrain => Assert.Equal(fewest, hand.Count(location => location.Terrain == terrain)));
        }
    }
}
