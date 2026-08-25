using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class ConfirmPassDoneTests
{
    [Fact]
    public void Should_start_play_when_the_last_human_confirms()
    {
        // Arrange
        GameState state = TestGame.Unwrap(Sut.ConfirmTokensTaken(TestGame.Dealing(5), 0));

        // Act
        GameState playing = TestGame.Unwrap(Sut.ConfirmPassDone(state, 0));

        // Assert
        Assert.Equal(GamePhase.InProgress, playing.Phase);
        Assert.Equal(1, playing.TurnNumber);
        Assert.Equal(0, playing.ActiveSeat);
    }

    [Fact]
    public void Should_keep_waiting_when_another_human_has_not_confirmed()
    {
        // Arrange
        GameState state = TestGame.Dealing(5, humans: 2);
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 1));

        // Act
        GameState passing = TestGame.Unwrap(Sut.ConfirmPassDone(state, 0));

        // Assert
        Assert.Equal(GamePhase.Passing, passing.Phase);
    }

    [Fact]
    public void Should_reject_the_confirmation_when_the_seat_has_already_confirmed()
    {
        // Arrange
        GameState state = TestGame.Dealing(5, humans: 2);
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 1));
        state = TestGame.Unwrap(Sut.ConfirmPassDone(state, 0));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ConfirmPassDone(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.AlreadyConfirmed, code);
    }

    [Fact]
    public void Should_reject_the_confirmation_when_the_game_never_had_a_passing_phase()
    {
        // Arrange
        GameState state = TestGame.InProgress(3);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ConfirmPassDone(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
