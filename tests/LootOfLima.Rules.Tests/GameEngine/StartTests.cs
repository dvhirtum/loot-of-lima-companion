using Setup = LootOfLima.Rules.Deal;
using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class StartTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_hand_every_seat_its_tokens_when_the_game_starts(int playerCount)
    {
        // Arrange
        GameState lobby = TestGame.Lobby(playerCount);
        Setup deal = TestGame.Deal(playerCount);

        // Act
        GameState state = TestGame.Unwrap(Sut.Start(lobby, deal, TestGame.Shuffle()));

        // Assert
        Assert.Equal(GamePhase.Dealing, state.Phase);
        Assert.All(state.Seats, seat => Assert.Equal(deal.Hands[seat.Index], seat.Hand));
    }

    [Fact]
    public void Should_confirm_the_bots_when_the_game_starts()
    {
        // Arrange
        // Bots have no screen to press a button on, so they can never be the thing being waited for.
        GameState lobby = TestGame.Lobby(5, humans: 2);

        // Act
        GameState state = TestGame.Unwrap(Sut.Start(lobby, TestGame.Deal(5), TestGame.Shuffle()));

        // Assert
        Assert.All(state.Seats.Where(seat => !seat.IsHuman), seat => Assert.True(seat.TokensConfirmed));
        Assert.All(state.HumanSeats, seat => Assert.False(seat.TokensConfirmed));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 10)]
    public void Should_plan_the_passes_when_the_deal_is_generated(int playerCount, int expectedPasses)
    {
        // Arrange
        GameState lobby = TestGame.Lobby(playerCount);

        // Act
        GameState state = TestGame.Unwrap(
            Sut.Start(lobby, TestGame.Deal(playerCount, DealSourceKind.Generated), TestGame.Shuffle()));

        // Assert
        Assert.Equal(expectedPasses, state.PendingPasses.Count);
    }

    [Fact]
    public void Should_plan_no_passes_when_there_are_three_players()
    {
        // Arrange
        GameState lobby = TestGame.Lobby(3);

        // Act
        GameState state = TestGame.Unwrap(Sut.Start(lobby, TestGame.Deal(3), TestGame.Shuffle()));

        // Assert
        Assert.Empty(state.PendingPasses);
    }

    [Fact]
    public void Should_plan_no_passes_when_the_deal_comes_from_a_printed_scenario()
    {
        // Arrange
        GameState lobby = TestGame.Lobby(5);
        Setup deal = TestGame.Deal(5, DealSourceKind.Scenario);

        // Act
        GameState state = TestGame.Unwrap(Sut.Start(lobby, deal, TestGame.Shuffle()));

        // Assert
        Assert.Empty(state.PendingPasses);
    }

    [Fact]
    public void Should_reject_the_start_when_the_deal_is_for_a_different_table()
    {
        // Arrange
        GameState lobby = TestGame.Lobby(4);
        Setup deal = TestGame.Deal(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.Start(lobby, deal, TestGame.Shuffle()));

        // Assert
        Assert.Equal(GameErrorCode.DealDoesNotFitGame, code);
    }

    [Fact]
    public void Should_reject_the_start_when_nobody_is_human()
    {
        // Arrange
        // Bots only answer, so a table of nothing but bots could never take a turn.
        GameState lobby = TestGame.Lobby(5, humans: 0);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.Start(lobby, TestGame.Deal(5), TestGame.Shuffle()));

        // Assert
        Assert.Equal(GameErrorCode.NoHumanSeats, code);
    }

    [Fact]
    public void Should_reject_the_start_when_the_game_has_already_started()
    {
        // Arrange
        GameState state = TestGame.Dealing(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.Start(state, TestGame.Deal(5), TestGame.Shuffle()));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
