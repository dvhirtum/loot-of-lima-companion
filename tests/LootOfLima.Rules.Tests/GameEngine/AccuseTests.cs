using Board = LootOfLima.Rules.BoardLayout;
using Location = LootOfLima.Rules.LocationId;
using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class AccuseTests
{
    [Fact]
    public void Should_win_the_game_when_the_accusation_names_the_treasure()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);
        IReadOnlyList<Location> treasure = state.Deal!.Treasure;

        // Act
        GameState finished = TestGame.Unwrap(Sut.Accuse(state, 0, treasure[0], treasure[1]));

        // Assert
        Assert.Equal(GamePhase.Finished, finished.Phase);
        Assert.Equal(0, finished.WinnerSeat);
        Assert.Equal(GameEndReason.CorrectAccusation, finished.EndReason);
        Assert.Null(finished.ActiveSeat);
    }

    [Fact]
    public void Should_win_the_game_when_the_treasure_is_named_in_the_other_order()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);
        IReadOnlyList<Location> treasure = state.Deal!.Treasure;

        // Act
        GameState finished = TestGame.Unwrap(Sut.Accuse(state, 0, treasure[1], treasure[0]));

        // Assert
        Assert.Equal(GameEndReason.CorrectAccusation, finished.EndReason);
    }

    [Fact]
    public void Should_eliminate_the_accuser_when_the_accusation_is_wrong()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 3);

        // Act
        GameState next = TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Assert
        Assert.True(next.SeatAt(0).Eliminated);
        Assert.Equal(GamePhase.InProgress, next.Phase);
    }

    [Fact]
    public void Should_reveal_nothing_when_the_accusation_is_wrong()
    {
        // Arrange
        // The rulebook is explicit: announce the failure and reveal nothing. The engine's part of
        // that is that no hand, and no part of the treasure, changes hands or is written anywhere.
        GameState state = TestGame.InProgress(5, humans: 3);

        // Act
        GameState next = TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Assert
        Assert.All(next.Seats, seat => Assert.Equal(state.SeatAt(seat.Index).Hand, seat.Hand));
        Assert.Equal(state.Deal!.Treasure, next.Deal!.Treasure);
        Assert.Null(next.WinnerSeat);
    }

    [Fact]
    public void Should_pass_the_turn_on_when_the_accuser_was_the_one_playing()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 3);

        // Act
        GameState next = TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Assert
        Assert.Equal(1, next.ActiveSeat);
    }

    [Fact]
    public void Should_leave_the_turn_alone_when_the_accusation_comes_from_off_turn()
    {
        // Arrange
        // Accusing is legal at any moment, even when it is somebody else's turn.
        GameState state = TestGame.InProgress(5, humans: 3);

        // Act
        GameState next = TestGame.Unwrap(
            Sut.Accuse(state, 2, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Assert
        Assert.Equal(0, next.ActiveSeat);
        Assert.Equal(1, next.TurnNumber);
        Assert.True(next.SeatAt(2).Eliminated);
    }

    [Fact]
    public void Should_skip_the_eliminated_seat_when_the_turn_comes_back_around()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 3);
        state = TestGame.Unwrap(
            Sut.Accuse(state, 1, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Act
        GameState next = TestGame.Unwrap(
            Sut.AskQuestion(state, 0, 4, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(2, next.ActiveSeat);
    }

    [Fact]
    public void Should_hand_the_win_to_the_survivor_when_everybody_else_has_been_eliminated()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 2);

        // Act
        GameState finished = TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Assert
        Assert.Equal(GamePhase.Finished, finished.Phase);
        Assert.Equal(1, finished.WinnerSeat);
        Assert.Equal(GameEndReason.LastPlayerStanding, finished.EndReason);
    }

    [Fact]
    public void Should_end_with_nobody_winning_when_the_only_human_is_eliminated()
    {
        // Arrange
        // A solo game against bots: a wrong accusation is simply a loss, since bots never win.
        GameState state = TestGame.InProgress(5, humans: 1);

        // Act
        GameState finished = TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Assert
        Assert.Equal(GamePhase.Finished, finished.Phase);
        Assert.Null(finished.WinnerSeat);
        Assert.Equal(GameEndReason.AllPlayersEliminated, finished.EndReason);
    }

    [Fact]
    public void Should_record_the_accusation_in_the_history_when_it_is_made()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 3);
        Location first = TestGame.NotTreasure(state);
        Location second = TestGame.NotTreasure(state, skip: 1);

        // Act
        GameState next = TestGame.Unwrap(Sut.Accuse(state, 0, first, second));

        // Assert
        AccusationEntry entry = Assert.Single(next.History.OfType<AccusationEntry>());
        Assert.Equal(0, entry.AccuserSeat);
        Assert.False(entry.Correct);
        Assert.Equal(new[] { first, second }.Order(), entry.Named);
    }

    [Fact]
    public void Should_close_the_history_with_the_ending_when_the_game_finishes()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameState finished = TestGame.Unwrap(
            Sut.Accuse(state, 0, state.Deal!.Treasure[0], state.Deal.Treasure[1]));

        // Assert
        GameEndedEntry ended = Assert.IsType<GameEndedEntry>(finished.History[^1]);
        Assert.Equal(0, ended.WinnerSeat);
        Assert.Equal(GameEndReason.CorrectAccusation, ended.Reason);
    }

    [Fact]
    public void Should_reject_the_accusation_when_it_names_one_location_twice()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);
        Location location = TestGame.NotTreasure(state);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.Accuse(state, 0, location, location));

        // Assert
        Assert.Equal(GameErrorCode.InvalidAccusation, code);
    }

    [Fact]
    public void Should_reject_the_accusation_when_a_named_location_is_not_on_the_island()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.Accuse(state, 0, default, TestGame.NotTreasure(state)));

        // Assert
        Assert.Equal(GameErrorCode.InvalidAccusation, code);
    }

    [Fact]
    public void Should_reject_the_accusation_when_the_accuser_is_already_eliminated()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 3);
        state = TestGame.Unwrap(
            Sut.Accuse(state, 2, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.Accuse(state, 2, state.Deal!.Treasure[0], state.Deal.Treasure[1]));

        // Assert
        Assert.Equal(GameErrorCode.SeatIsEliminated, code);
    }

    [Fact]
    public void Should_reject_the_accusation_when_the_seat_is_a_bot()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.Accuse(state, 4, state.Deal!.Treasure[0], state.Deal.Treasure[1]));

        // Assert
        Assert.Equal(GameErrorCode.SeatIsNotHuman, code);
    }

    [Fact]
    public void Should_reject_the_accusation_when_the_game_is_already_over()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 2);
        state = TestGame.Unwrap(Sut.Accuse(state, 0, state.Deal!.Treasure[0], state.Deal.Treasure[1]));

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.Accuse(state, 1, state.Deal!.Treasure[0], state.Deal.Treasure[1]));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
