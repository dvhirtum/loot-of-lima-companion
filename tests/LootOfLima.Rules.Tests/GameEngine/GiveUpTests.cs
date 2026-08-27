using Board = LootOfLima.Rules.BoardLayout;
using Location = LootOfLima.Rules.LocationId;
using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class GiveUpTests
{
    /// <summary>A solo game with one wrong accusation already behind it.</summary>
    private static GameState Stuck()
    {
        GameState state = TestGame.InProgress(5, humans: 1);

        return TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));
    }

    [Fact]
    public void Should_finish_the_game_when_the_only_player_gives_up()
    {
        // Arrange
        GameState state = Stuck();

        // Act
        GameState finished = TestGame.Unwrap(Sut.GiveUp(state, 0));

        // Assert
        Assert.Equal(GamePhase.Finished, finished.Phase);
        Assert.Equal(GameEndReason.Conceded, finished.EndReason);
        Assert.Null(finished.WinnerSeat);
        Assert.Null(finished.ActiveSeat);
    }

    [Fact]
    public void Should_close_the_history_with_the_ending_when_the_only_player_gives_up()
    {
        // Arrange
        GameState state = Stuck();

        // Act
        GameState finished = TestGame.Unwrap(Sut.GiveUp(state, 0));

        // Assert
        GameEndedEntry ended = Assert.IsType<GameEndedEntry>(finished.History[^1]);
        Assert.Null(ended.WinnerSeat);
        Assert.Equal(GameEndReason.Conceded, ended.Reason);
    }

    [Fact]
    public void Should_reject_giving_up_when_the_seat_has_not_yet_accused()
    {
        // Arrange
        // The treasure is the one thing this application exists to withhold, so the command that
        // hands it over is not available for the asking. You have to have named a pair and been
        // told it was wrong.
        GameState state = TestGame.InProgress(5, humans: 1);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.GiveUp(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.NothingToGiveUpOn, code);
    }

    [Fact]
    public void Should_reject_giving_up_when_more_than_one_human_is_at_the_table()
    {
        // Arrange
        // Two humans have a way out already: accusing wrongly, which ends their game and leaves the
        // other one playing. Conceding here would end somebody else's game as well.
        GameState state = TestGame.InProgress(5, humans: 3);
        state = TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.GiveUp(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.NotASoloGame, code);
    }

    [Fact]
    public void Should_reject_giving_up_when_the_game_has_not_started()
    {
        // Arrange
        GameState state = TestGame.Lobby(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.GiveUp(state, 0));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }

    [Fact]
    public void Should_reject_giving_up_when_the_seat_is_a_bot()
    {
        // Arrange
        GameState state = Stuck();

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.GiveUp(state, 4));

        // Assert
        Assert.Equal(GameErrorCode.SeatIsNotHuman, code);
    }

    [Fact]
    public void Should_reject_giving_up_when_the_game_is_already_over()
    {
        // Arrange
        GameState state = Stuck();
        GameState finished = TestGame.Unwrap(Sut.GiveUp(state, 0));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.GiveUp(finished, 0));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }

    [Fact]
    public void Should_still_allow_a_correct_accusation_when_the_player_has_not_given_up()
    {
        // Arrange
        // Giving up is an offer, not a fork the game is stuck in: the ordinary way to win is still
        // there after a wrong guess, and the question loop still runs.
        GameState state = Stuck();

        // Act
        GameState next = TestGame.Unwrap(
            Sut.AskQuestion(state, 0, 4, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GamePhase.InProgress, next.Phase);
        Assert.Equal(0, next.ActiveSeat);
    }

    [Fact]
    public void Should_write_no_ending_when_a_solo_game_is_left_running()
    {
        // Arrange
        // The engine's half of the promise the projection keeps: until an ending is written the
        // phase is not Finished, and until the phase is Finished there is nothing for a view to
        // read the treasure out of.
        GameState state = TestGame.InProgress(5, humans: 1);
        IReadOnlyList<Location> treasure = state.Deal!.Treasure;

        // Act
        GameState next = TestGame.Unwrap(
            Sut.Accuse(state, 0, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Assert
        Assert.Empty(next.History.OfType<GameEndedEntry>());
        Assert.DoesNotContain(
            next.History.OfType<AccusationEntry>().SelectMany(entry => entry.Named),
            named => treasure.Contains(named));
    }
}
