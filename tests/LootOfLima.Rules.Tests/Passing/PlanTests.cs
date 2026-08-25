using Deals = LootOfLima.Rules.Dealer;
using Location = LootOfLima.Rules.LocationId;
using Setup = LootOfLima.Rules.Deal;
using Sut = LootOfLima.Rules.Passing;

namespace LootOfLima.Rules.Tests.Passing;

public class PlanTests
{
    [Fact]
    public void Should_plan_nothing_when_there_are_three_players()
    {
        // Arrange
        Setup deal = Deals.Create(3, TestGame.DefaultSeed);

        // Act
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Assert
        Assert.Empty(passes);
    }

    [Fact]
    public void Should_pass_one_token_straight_across_when_there_are_four_players()
    {
        // Arrange
        Setup deal = Deals.Create(4, TestGame.DefaultSeed);

        // Act
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Assert
        Assert.Equal(4, passes.Count);
        Assert.All(passes, pass => Assert.Equal(PassDirection.Across, pass.Direction));
        Assert.All(passes, pass => Assert.Equal((pass.GiverSeat + 2) % 4, pass.ReceiverSeat));
        Assert.Equal([0, 1, 2, 3], passes.Select(pass => pass.GiverSeat).Order());
    }

    [Fact]
    public void Should_pass_once_right_and_once_left_when_there_are_five_players()
    {
        // Arrange
        Setup deal = Deals.Create(5, TestGame.DefaultSeed);

        // Act
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Assert
        Assert.Equal(10, passes.Count);

        List<PassRecord> right = [.. passes.Where(pass => pass.Direction == PassDirection.Right)];
        List<PassRecord> left = [.. passes.Where(pass => pass.Direction == PassDirection.Left)];

        Assert.Equal(5, right.Count);
        Assert.Equal(5, left.Count);
        Assert.All(right, pass => Assert.Equal((pass.GiverSeat + 1) % 5, pass.ReceiverSeat));
        Assert.All(left, pass => Assert.Equal((pass.GiverSeat + 4) % 5, pass.ReceiverSeat));
    }

    [Fact]
    public void Should_plan_every_right_pass_before_any_left_pass_when_there_are_five_players()
    {
        // Arrange
        // The order matters: Apply walks the plan in sequence, and a seat chooses its left token
        // from what it was dealt, not from what has just arrived.
        Setup deal = Deals.Create(5, TestGame.DefaultSeed);

        // Act
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Assert
        Assert.All(passes.Take(5), pass => Assert.Equal(PassDirection.Right, pass.Direction));
        Assert.All(passes.Skip(5), pass => Assert.Equal(PassDirection.Left, pass.Direction));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_only_pass_a_token_the_giver_was_dealt_when_the_plan_is_made(int playerCount)
    {
        // Arrange
        Setup deal = Deals.Create(playerCount, TestGame.DefaultSeed);

        // Act
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Assert
        Assert.All(passes, pass => Assert.Contains(pass.Location, deal.Hands[pass.GiverSeat]));
    }

    [Fact]
    public void Should_pass_two_different_tokens_when_a_five_player_seat_passes_twice()
    {
        // Arrange
        // The rulebook says to look at *another* token for the second pass. It also keeps the
        // knowledge honest: the token you sent right must still be there when play starts.
        Setup deal = Deals.Create(5, TestGame.DefaultSeed);

        // Act
        IReadOnlyList<PassRecord> passes = Sut.Plan(deal.Hands, TestGame.Shuffle());

        // Assert
        foreach (int seat in Enumerable.Range(0, 5))
        {
            List<Location> sent = [.. passes.Where(pass => pass.GiverSeat == seat).Select(pass => pass.Location)];
            Assert.Equal(2, sent.Count);
            Assert.Equal(2, sent.Distinct().Count());
        }
    }
}
