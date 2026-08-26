using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class ReleaseSeatTests
{
    [Fact]
    public void Should_hand_the_seat_back_to_a_bot_when_a_player_leaves()
    {
        // Arrange
        // The table always stays full, so a released seat becomes a bot rather than an empty chair.
        GameState state = TestGame.Lobby(5, humans: 2);

        // Act
        GameState released = TestGame.Unwrap(Sut.ReleaseSeat(state, 1));

        // Assert
        Assert.Equal(SeatKind.Bot, released.SeatAt(1).Kind);
        Assert.Null(released.SeatAt(1).PlayerId);
        Assert.Equal(5, released.PlayerCount);
    }

    [Fact]
    public void Should_reject_the_release_when_the_seat_was_never_claimed()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 1);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ReleaseSeat(state, 4));

        // Assert
        Assert.Equal(GameErrorCode.SeatNotClaimed, code);
    }

    [Fact]
    public void Should_reject_the_release_when_the_game_has_already_been_dealt()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ReleaseSeat(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
