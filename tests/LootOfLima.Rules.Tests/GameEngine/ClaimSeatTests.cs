using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class ClaimSeatTests
{
    [Fact]
    public void Should_seat_the_player_when_the_seat_is_still_a_bot()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 0);

        // Act
        GameState claimed = TestGame.Unwrap(Sut.ClaimSeat(state, 3, "player-abc", "Ada"));

        // Assert
        Assert.Equal(SeatKind.Human, claimed.SeatAt(3).Kind);
        Assert.Equal("Ada", claimed.SeatAt(3).DisplayName);
        Assert.Equal("player-abc", claimed.SeatAt(3).PlayerId);
    }

    [Fact]
    public void Should_leave_the_other_seats_alone_when_one_is_claimed()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 0);

        // Act
        GameState claimed = TestGame.Unwrap(Sut.ClaimSeat(state, 3, "player-abc", "Ada"));

        // Assert
        Assert.Single(claimed.HumanSeats);
    }

    [Fact]
    public void Should_reject_the_claim_when_the_seat_is_already_taken()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 1);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ClaimSeat(state, 0, "player-abc", "Ada"));

        // Assert
        Assert.Equal(GameErrorCode.SeatAlreadyClaimed, code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Should_reject_the_claim_when_there_is_no_such_seat(int seatIndex)
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 0);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ClaimSeat(state, seatIndex, "player-abc", "Ada"));

        // Assert
        Assert.Equal(GameErrorCode.SeatOutOfRange, code);
    }

    [Fact]
    public void Should_reject_the_claim_when_the_game_has_already_been_dealt()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ClaimSeat(state, 4, "player-abc", "Ada"));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
