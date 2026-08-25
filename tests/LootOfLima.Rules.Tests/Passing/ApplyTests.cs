using Deals = LootOfLima.Rules.Dealer;
using Location = LootOfLima.Rules.LocationId;
using Setup = LootOfLima.Rules.Deal;
using Sut = LootOfLima.Rules.Passing;

namespace LootOfLima.Rules.Tests.Passing;

public class ApplyTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_leave_every_hand_the_same_size_when_the_passes_are_applied(int playerCount)
    {
        // Arrange
        Setup deal = Deals.Create(playerCount, TestGame.DefaultSeed);
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Act
        IReadOnlyList<IReadOnlyList<Location>> hands = Sut.Apply(deal.Hands, passes);

        // Assert
        Assert.Equal(deal.Hands.Select(hand => hand.Count), hands.Select(hand => hand.Count));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_move_no_token_off_the_table_when_the_passes_are_applied(int playerCount)
    {
        // Arrange
        Setup deal = Deals.Create(playerCount, TestGame.DefaultSeed);
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Act
        IReadOnlyList<IReadOnlyList<Location>> hands = Sut.Apply(deal.Hands, passes);

        // Assert
        Assert.Equal(
            deal.Hands.SelectMany(hand => hand).Order(),
            hands.SelectMany(hand => hand).Order());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_leave_the_passed_token_with_its_receiver_when_all_the_passes_are_applied(int playerCount)
    {
        // Arrange
        // This is the whole point of passing: the giver ends up knowing one location a named
        // opponent holds. If a later pass could move it on again, that knowledge would be a lie.
        Setup deal = Deals.Create(playerCount, TestGame.DefaultSeed);
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Act
        IReadOnlyList<IReadOnlyList<Location>> hands = Sut.Apply(deal.Hands, passes);

        // Assert
        Assert.All(passes, pass => Assert.Contains(pass.Location, hands[pass.ReceiverSeat]));
    }

    [Fact]
    public void Should_sort_every_hand_when_the_passes_are_applied()
    {
        // Arrange
        Setup deal = Deals.Create(5, TestGame.DefaultSeed);
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Act
        IReadOnlyList<IReadOnlyList<Location>> hands = Sut.Apply(deal.Hands, passes);

        // Assert
        Assert.All(hands, hand => Assert.Equal(hand.Order(), hand));
    }

    [Fact]
    public void Should_throw_when_a_seat_passes_a_token_it_does_not_hold()
    {
        // Arrange
        Setup deal = Deals.Create(4, TestGame.DefaultSeed);
        Location notHeld = deal.Treasure[0];
        IReadOnlyList<PassRecord> passes = [new PassRecord(0, 2, notHeld, PassDirection.Across)];

        // Act
        ArgumentException exception = Assert.Throws<ArgumentException>(() => Sut.Apply(deal.Hands, passes));

        // Assert
        Assert.Equal("passes", exception.ParamName);
    }
}
