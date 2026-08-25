using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class ConfirmTokensTakenTests
{
    [Fact]
    public void Should_keep_waiting_when_another_human_has_not_confirmed()
    {
        // Arrange
        GameState state = TestGame.Dealing(5, humans: 2, kind: DealSourceKind.Scenario);

        // Act
        GameState confirmed = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));

        // Assert
        Assert.Equal(GamePhase.Dealing, confirmed.Phase);
        Assert.True(confirmed.SeatAt(0).TokensConfirmed);
    }

    [Fact]
    public void Should_start_play_when_the_deal_settles_the_passing_and_the_announcements_itself()
    {
        // Arrange
        // A scenario deal has no passing and carries the book's printed "fewest terrain" letters,
        // so both setup phases resolve without anybody being asked anything.
        GameState state = TestGame.Dealing(5, humans: 2, kind: DealSourceKind.Scenario);
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));

        // Act
        GameState playing = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 1));

        // Assert
        Assert.Equal(GamePhase.InProgress, playing.Phase);
        Assert.Equal(1, playing.TurnNumber);
        Assert.Equal(0, playing.ActiveSeat);
        Assert.All(playing.Seats, seat => Assert.NotNull(seat.FewestTerrain));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_move_to_passing_when_the_last_human_confirms_and_the_deal_was_generated(int playerCount)
    {
        // Arrange
        GameState state = TestGame.Dealing(playerCount);

        // Act
        GameState passing = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));

        // Assert
        Assert.Equal(GamePhase.Passing, passing.Phase);
        Assert.Empty(passing.PendingPasses);
    }

    [Fact]
    public void Should_move_the_planned_tokens_when_passing_begins()
    {
        // Arrange
        GameState state = TestGame.Dealing(5);
        IReadOnlyList<PassRecord> planned = state.PendingPasses;

        // Act
        GameState passing = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));

        // Assert
        Assert.All(planned, pass => Assert.Contains(pass.Location, passing.SeatAt(pass.ReceiverSeat).Hand));
        Assert.All(planned, pass => Assert.DoesNotContain(pass.Location, passing.SeatAt(pass.GiverSeat).Hand));
    }

    [Fact]
    public void Should_record_every_pass_in_the_history_when_passing_begins()
    {
        // Arrange
        GameState state = TestGame.Dealing(5);

        // Act
        GameState passing = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));

        // Assert
        Assert.Equal(10, passing.History.OfType<TokenPassEntry>().Count());
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9], passing.History.Select(entry => entry.Sequence));
    }

    [Fact]
    public void Should_reject_the_confirmation_when_the_seat_has_already_confirmed()
    {
        // Arrange
        GameState state = TestGame.Dealing(5, humans: 2, kind: DealSourceKind.Scenario);
        state = TestGame.Unwrap(Sut.ConfirmTokensTaken(state, 0));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ConfirmTokensTaken(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.AlreadyConfirmed, code);
    }

    [Fact]
    public void Should_reject_the_confirmation_when_the_seat_is_a_bot()
    {
        // Arrange
        GameState state = TestGame.Dealing(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ConfirmTokensTaken(state, 4));

        // Assert
        Assert.Equal(GameErrorCode.SeatIsNotHuman, code);
    }

    [Fact]
    public void Should_reject_the_confirmation_when_the_game_is_still_in_the_lobby()
    {
        // Arrange
        GameState state = TestGame.Lobby(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.ConfirmTokensTaken(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
