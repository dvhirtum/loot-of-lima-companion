using Location = LootOfLima.Rules.LocationId;
using Setup = LootOfLima.Rules.Deal;
using Sut = LootOfLima.Rules.Dealer;

namespace LootOfLima.Rules.Tests.Dealer;

public class CreateTests
{
    [Theory]
    [InlineData(3, 7, 1)]
    [InlineData(4, 5, 2)]
    [InlineData(5, 4, 2)]
    public void Should_deal_the_rulebook_hand_and_reveal_when_the_table_is_this_size(
        int playerCount,
        int handSize,
        int revealCount)
    {
        // Arrange
        // Act
        Setup deal = Sut.Create(playerCount, TestGame.DefaultSeed);

        // Assert
        Assert.Equal(playerCount, deal.PlayerCount);
        Assert.All(deal.Hands, hand => Assert.Equal(handSize, hand.Count));
        Assert.Equal(revealCount, deal.PublicReveal.Count);
        Assert.Equal(2, deal.Treasure.Count);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_partition_all_24_locations_when_the_island_is_dealt(int playerCount)
    {
        // Arrange
        Setup deal = Sut.Create(playerCount, TestGame.DefaultSeed);

        // Act
        List<Location> everything =
            [.. deal.Hands.SelectMany(hand => hand).Concat(deal.PublicReveal).Concat(deal.Treasure)];

        // Assert
        Assert.Equal(Location.TotalCount, everything.Count);
        Assert.Equal(Location.All.Order(), everything.Order());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_deal_the_same_island_when_the_seed_is_repeated(int playerCount)
    {
        // Arrange
        Setup first = Sut.Create(playerCount, 4242);

        // Act
        Setup second = Sut.Create(playerCount, 4242);

        // Assert
        Assert.Equal(first.Treasure, second.Treasure);
        Assert.Equal(first.PublicReveal, second.PublicReveal);
        Assert.Equal(first.Hands, second.Hands);
    }

    [Fact]
    public void Should_deal_a_different_island_when_the_seed_differs()
    {
        // Arrange
        Setup first = Sut.Create(5, 1);

        // Act
        Setup second = Sut.Create(5, 2);

        // Assert
        Assert.NotEqual(first.Hands[0], second.Hands[0]);
    }

    [Fact]
    public void Should_record_the_seed_when_the_deal_is_generated()
    {
        // Arrange
        // Act
        Setup deal = Sut.Create(5, 99);

        // Assert
        Assert.Equal(DealSourceKind.Generated, deal.Source.Kind);
        Assert.Equal("99", deal.Source.Reference);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void Should_throw_when_the_table_is_the_wrong_size(int playerCount)
    {
        // Arrange
        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => Sut.Create(playerCount, TestGame.DefaultSeed));

        // Assert
        Assert.Equal("playerCount", exception.ParamName);
    }

    [Fact]
    public void Should_use_the_supplied_shuffle_when_one_is_injected()
    {
        // Arrange
        // Answering "the last index" makes every Fisher-Yates swap a self-swap, so the pile stays
        // in index order and the whole deal is predictable off the top of the head.
        IdentityShuffle shuffle = new();
        Location[] expectedTreasure = [Location.Parse("B1"), Location.Parse("B2")];
        Location[] expectedFirstHand =
            [Location.Parse("B3"), Location.Parse("B4"), Location.Parse("B5"), Location.Parse("B6")];

        // Act
        Setup deal = Sut.Create(5, shuffle, seed: 0);

        // Assert
        Assert.Equal(expectedTreasure, deal.Treasure);
        Assert.Equal(expectedFirstHand, deal.Hands[0]);
    }

    private sealed class IdentityShuffle : IShuffleSource
    {
        public int Next(int exclusiveUpperBound) => exclusiveUpperBound - 1;
    }
}
