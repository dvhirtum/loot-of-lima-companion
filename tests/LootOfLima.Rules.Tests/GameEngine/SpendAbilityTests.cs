using Sut = LootOfLima.Rules.GameEngine;

namespace LootOfLima.Rules.Tests.GameEngine;

public class SpendAbilityTests
{
    [Theory]
    [InlineData(AbilityType.Barrel)]
    [InlineData(AbilityType.Shovel)]
    [InlineData(AbilityType.Pistol)]
    public void Should_take_the_token_off_the_board_when_an_ability_is_spent(AbilityType ability)
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameState spent = TestGame.Unwrap(Sut.SpendAbility(state, 0, ability));

        // Assert
        Assert.DoesNotContain(ability, spent.SeatAt(0).AbilitiesRemaining);
        Assert.Equal(2, spent.SeatAt(0).AbilitiesRemaining.Count);
    }

    [Fact]
    public void Should_record_the_spend_in_the_history_when_an_ability_is_spent()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameState spent = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Barrel));

        // Assert
        AbilityUsedEntry entry = Assert.Single(spent.History.OfType<AbilityUsedEntry>());
        Assert.Equal(0, entry.SeatIndex);
        Assert.Equal(AbilityType.Barrel, entry.Ability);
        Assert.Equal(1, entry.TurnNumber);
    }

    [Theory]
    [InlineData(AbilityType.Barrel)]
    [InlineData(AbilityType.Shovel)]
    public void Should_change_nothing_else_when_a_dice_ability_is_spent(AbilityType ability)
    {
        // Arrange
        // The app does not model the roll, so Barrel and Shovel act only on the physical dice.
        GameState state = TestGame.InProgress(5);

        // Act
        GameState spent = TestGame.Unwrap(Sut.SpendAbility(state, 0, ability));

        // Assert
        Assert.Equal(state.ActiveSeat, spent.ActiveSeat);
        Assert.Equal(state.TurnNumber, spent.TurnNumber);
        Assert.All(spent.Seats, seat => Assert.Equal(state.SeatAt(seat.Index).Hand, seat.Hand));
        Assert.Null(spent.SeatAt(0).PistolArmedTurn);
    }

    [Fact]
    public void Should_arm_the_secret_question_when_the_pistol_is_spent()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameState spent = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Pistol));

        // Assert
        Assert.Equal(spent.TurnNumber, spent.SeatAt(0).PistolArmedTurn);
    }

    [Fact]
    public void Should_reject_the_spend_when_the_ability_is_already_gone()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);
        state = TestGame.Unwrap(Sut.SpendAbility(state, 0, AbilityType.Barrel));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.SpendAbility(state, 0, AbilityType.Barrel));

        // Assert
        Assert.Equal(GameErrorCode.AbilityAlreadySpent, code);
    }

    [Fact]
    public void Should_reject_the_spend_when_it_is_not_that_seat_turn()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 2);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.SpendAbility(state, 1, AbilityType.Barrel));

        // Assert
        Assert.Equal(GameErrorCode.NotYourTurn, code);
    }

    [Fact]
    public void Should_reject_the_spend_when_the_seat_is_a_bot()
    {
        // Arrange
        GameState state = TestGame.InProgress(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.SpendAbility(state, 3, AbilityType.Barrel));

        // Assert
        Assert.Equal(GameErrorCode.SeatIsNotHuman, code);
    }

    [Fact]
    public void Should_reject_the_spend_when_the_seat_has_been_eliminated()
    {
        // Arrange
        GameState state = TestGame.InProgress(5, humans: 3);
        state = TestGame.Unwrap(
            Sut.Accuse(state, 1, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.SpendAbility(state, 1, AbilityType.Barrel));

        // Assert
        Assert.Equal(GameErrorCode.SeatIsEliminated, code);
    }

    [Fact]
    public void Should_reject_the_spend_when_the_game_is_not_under_way()
    {
        // Arrange
        GameState state = TestGame.Dealing(5);

        // Act
        GameErrorCode code = TestGame.Rejection(Sut.SpendAbility(state, 0, AbilityType.Barrel));

        // Assert
        Assert.Equal(GameErrorCode.WrongPhase, code);
    }
}
