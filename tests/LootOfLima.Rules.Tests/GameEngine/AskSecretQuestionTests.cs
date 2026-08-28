using Board = LootOfLima.Rules.BoardLayout;
using Counter = LootOfLima.Rules.Engine;
using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class AskSecretQuestionTests
{
    [Fact]
    public void Should_answer_from_the_target_hand_when_the_pistol_was_spent_this_turn()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);
        state = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Pistol));
        QueryRange range = new(Direction.NE, Direction.SW);
        int expected = Counter.CountInRange(state.SeatAt(2).Hand, range, Board.Default);

        // Act
        GameState asked = TestGame.Unwrap(Sut.AskSecretQuestion(state, 0, 2, range, Board.Default));

        // Assert
        SecretQuestionEntry entry = Assert.Single(asked.History.OfType<SecretQuestionEntry>());
        Assert.Equal(expected, entry.Answer);
        Assert.Equal(0, entry.AskerSeat);
        Assert.Equal(2, entry.TargetSeat);
    }

    [Fact]
    public void Should_pass_the_turn_on_when_a_secret_question_has_been_asked()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 2);
        state = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Pistol));

        // Act
        GameState asked = TestGame.Unwrap(
            Sut.AskSecretQuestion(state, 0, 2, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(1, asked.ActiveSeat);
        Assert.Equal(2, asked.TurnNumber);
    }

    [Fact]
    public void Should_reject_the_question_when_no_pistol_was_spent()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskSecretQuestion(state, 0, 2, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.PistolNotSpentThisTurn, code);
    }

    [Fact]
    public void Should_reject_the_question_when_the_pistol_was_spent_on_an_earlier_turn()
    {
        // Arrange
        // The Pistol unlocks exactly one question on exactly one turn.
        GameState state = TestGame.InProgress(5);
        state = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Pistol));
        state = TestGame.Unwrap(
            Sut.AskQuestion(state, 0, 1, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskSecretQuestion(state, 0, 2, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.PistolNotSpentThisTurn, code);
    }

    [Theory]
    [InlineData(Terrain.Beach)]
    [InlineData(Terrain.Forest)]
    [InlineData(Terrain.Mountain)]
    public void Should_answer_within_one_terrain_when_the_question_names_one(Terrain terrain)
    {
        // Arrange
        // The Pistol lifts the roll, not the shapes a question may take: a single terrain is a
        // question a roll could have allowed, so it is allowed here and answered like any other.
        GameState state = TestGame.InProgress(5);
        state = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Pistol));
        QueryRange range = new(Direction.N, Direction.S, terrain);
        int expected = Counter.CountInRange(state.SeatAt(2).Hand, range, Board.Default);

        // Act
        GameState asked = TestGame.Unwrap(Sut.AskSecretQuestion(state, 0, 2, range, Board.Default));

        // Assert
        SecretQuestionEntry entry = Assert.Single(asked.History.OfType<SecretQuestionEntry>());
        Assert.Equal(terrain, entry.Range.TerrainFilter);
        Assert.Equal(expected, entry.Answer);
    }

    [Fact]
    public void Should_reject_the_question_when_it_is_not_that_seat_turn()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 2);

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskSecretQuestion(state, 1, 0, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.NotYourTurn, code);
    }

    [Fact]
    public void Should_reject_the_question_when_the_asker_targets_themselves()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);
        state = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Pistol));

        // Act
        GameErrorCode code = TestGame.Rejection(
            Sut.AskSecretQuestion(state, 0, 0, new QueryRange(Direction.N, Direction.S), Board.Default));

        // Assert
        Assert.Equal(GameErrorCode.CannotTargetYourself, code);
    }
}
