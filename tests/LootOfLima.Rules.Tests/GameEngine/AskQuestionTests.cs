using Board = LootOfLima.Rules.BoardLayout;
using Counter = LootOfLima.Rules.Engine;
using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class AskQuestionTests
{
    [Fact]
    public void Should_answer_from_the_hand_the_app_dealt_when_a_question_is_asked()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);
        QueryRange range = new(Direction.N, Direction.S);
        int expected = Counter.CountInRange(state.SeatAt(2).Hand, range, Board.Default);

        // Act
        GameState asked = TestGame.Unwrap(Sut.AskQuestion(state, 0, 2, range, Board.Default));

        // Assert
        PublicQuestionEntry entry = Assert.Single(asked.History.OfType<PublicQuestionEntry>());
        Assert.Equal(expected, entry.Answer);
        Assert.Equal(0, entry.AskerSeat);
        Assert.Equal(2, entry.TargetSeat);
        Assert.Equal(range, entry.Range);
    }

    [Fact]
    public void Should_answer_for_a_human_target_too_when_a_question_is_asked()
    {
        // Arrange
        // The app auto-answers for humans as well, from the hand it dealt them. No arithmetic slips.
        GameState state = TestGame.InProgress(5, humans: 2);
        QueryRange range = new(Direction.E, Direction.W, Terrain.Beach);
        int expected = Counter.CountInRange(state.SeatAt(1).Hand, range, Board.Default);

        // Act
        GameState asked = TestGame.Unwrap(Sut.AskQuestion(state, 0, 1, range, Board.Default));

        // Assert
        Assert.Equal(expected, asked.History.OfType<PublicQuestionEntry>().Single().Answer);
    }

    [Fact]
    public void Should_pass_the_turn_on_when_a_question_has_been_asked()
    {
        // Arrange
        // A turn is one question, so asking ends it.
        GameState state = TestGame.InProgress(5, humans: 2);

        // Act
        GameState asked = TestGame.Unwrap(
            Sut.AskQuestion(state, 0, 3, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(1, asked.ActiveSeat);
        Assert.Equal(2, asked.TurnNumber);
    }

    [Fact]
    public void Should_skip_the_bots_when_the_turn_passes_on()
    {
        // Arrange
        // Bots never take turns; they only answer.
        GameState state = TestGame.InProgress(5, humans: 1);

        // Act
        GameState asked = TestGame.Unwrap(
            Sut.AskQuestion(state, 0, 4, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(0, asked.ActiveSeat);
        Assert.Equal(2, asked.TurnNumber);
    }

    [Fact]
    public void Should_accept_any_range_when_a_question_is_asked()
    {
        // Arrange
        // The app deliberately does not model the roll, so it never checks that the dice allowed
        // this question. Players self-police at the table, exactly as they always have.
        GameState state = TestGame.InProgress(5);

        // Act
        GameState asked = TestGame.Unwrap(
            Sut.AskQuestion(state, 0, 1, new QueryRange(Direction.NW, Direction.NE, Terrain.Mountain), Board.Default));

        // Assert
        Assert.Single(asked.History.OfType<PublicQuestionEntry>());
    }

    [Fact]
    public void Should_reject_the_question_when_it_is_not_that_seat_turn()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 2);

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskQuestion(state, 1, 0, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.NotYourTurn, code);
    }

    [Fact]
    public void Should_reject_the_question_when_the_asker_targets_themselves()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskQuestion(state, 0, 0, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.CannotTargetYourself, code);
    }

    [Fact]
    public void Should_reject_the_question_when_there_is_no_such_target()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskQuestion(state, 0, 9, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.SeatOutOfRange, code);
    }

    [Fact]
    public void Should_still_answer_when_the_target_has_been_eliminated()
    {
        // Arrange
        // A wrong accusation costs you your turns, not your obligation to answer.
        GameState state = TestGame.InProgress(5, humans: 3);
        state = TestGame.Unwrap(
            Sut.Accuse(state, 2, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        Assert.Equal(GamePhase.InProgress, state.Phase);
        Assert.True(state.SeatAt(2).Eliminated);

        // Act
        // Both arrows on the same line sweeps the whole circle, so the answer is the whole hand.
        GameState asked = TestGame.Unwrap(
            Sut.AskQuestion(state, 0, 2, new QueryRange(Direction.N, Direction.N), Board.Default));

        // Assert
        Assert.Equal(
            state.SeatAt(2).Hand.Count,
            asked.History.OfType<PublicQuestionEntry>().Single().Answer);
    }

    [Fact]
    public void Should_reject_the_question_when_the_game_is_not_under_way()
    {
        // Arrange
        GameState state = TestGame.Dealing(5);

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskQuestion(state, 0, 1, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
