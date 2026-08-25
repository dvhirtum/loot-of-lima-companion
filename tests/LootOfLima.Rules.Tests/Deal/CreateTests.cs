using Deals = LootOfLima.Rules.Dealer;
using Location = LootOfLima.Rules.LocationId;
using Sut = LootOfLima.Rules.Deal;

namespace LootOfLima.Rules.Tests.Deal;

public class CreateTests
{
    [Fact]
    public void Should_sort_every_hand_when_a_deal_is_created()
    {
        // Arrange
        Sut source = Deals.Create(5, TestGame.DefaultSeed);
        List<IReadOnlyList<Location>> shuffledHands =
            [.. source.Hands.Select(hand => (IReadOnlyList<Location>)[.. hand.OrderDescending()])];

        // Act
        Sut deal = Sut.Create(shuffledHands, source.PublicReveal, source.Treasure, source.Source);

        // Assert
        Assert.All(deal.Hands, hand => Assert.Equal(hand.Order(), hand));
    }

    [Fact]
    public void Should_throw_when_the_same_location_is_dealt_twice()
    {
        // Arrange
        Sut source = Deals.Create(5, TestGame.DefaultSeed);
        List<IReadOnlyList<Location>> hands = [.. source.Hands];

        // Give seat 1 a copy of one of seat 0 tokens, so a location appears in two hands.
        hands[1] = [source.Hands[0][0], .. source.Hands[1].Skip(1)];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Sut.Create(hands, source.PublicReveal, source.Treasure, source.Source));

        // Assert
        Assert.Contains("dealt more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_throw_when_a_hand_holds_a_location_that_is_also_face_up()
    {
        // Arrange
        Sut source = Deals.Create(5, TestGame.DefaultSeed);
        List<IReadOnlyList<Location>> hands = [.. source.Hands];

        // Swap one of seat 0 tokens for a copy of a face-up one. With every size still correct,
        // pigeonholing means a location can only go missing by another one appearing twice.
        hands[0] = [source.PublicReveal[0], .. source.Hands[0].Skip(1)];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Sut.Create(hands, source.PublicReveal, source.Treasure, source.Source));

        // Assert
        Assert.Contains("dealt more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_throw_when_a_hand_is_the_wrong_size()
    {
        // Arrange
        Sut source = Deals.Create(5, TestGame.DefaultSeed);
        List<IReadOnlyList<Location>> hands = [.. source.Hands];
        hands[0] = [.. source.Hands[0].Skip(1)];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Sut.Create(hands, source.PublicReveal, source.Treasure, source.Source));

        // Assert
        Assert.Equal("hands", exception.ParamName);
    }

    [Fact]
    public void Should_throw_when_the_reveal_is_the_wrong_size()
    {
        // Arrange
        Sut source = Deals.Create(5, TestGame.DefaultSeed);
        IReadOnlyList<Location> reveal = [source.PublicReveal[0]];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Sut.Create(source.Hands, reveal, source.Treasure, source.Source));

        // Assert
        Assert.Equal("publicReveal", exception.ParamName);
    }

    [Fact]
    public void Should_throw_when_the_treasure_is_not_two_locations()
    {
        // Arrange
        Sut source = Deals.Create(5, TestGame.DefaultSeed);
        IReadOnlyList<Location> treasure = [source.Treasure[0]];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Sut.Create(source.Hands, source.PublicReveal, treasure, source.Source));

        // Assert
        Assert.Equal("treasure", exception.ParamName);
    }

    [Fact]
    public void Should_throw_when_the_table_is_the_wrong_size()
    {
        // Arrange
        Sut source = Deals.Create(5, TestGame.DefaultSeed);
        IReadOnlyList<IReadOnlyList<Location>> hands = [.. source.Hands.Take(2)];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Sut.Create(hands, source.PublicReveal, source.Treasure, source.Source));

        // Assert
        Assert.Equal("hands", exception.ParamName);
    }
}
