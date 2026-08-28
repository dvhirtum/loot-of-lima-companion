using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

/// <summary>
/// What the view says about ability tokens, and about the one of them that has an effect the client
/// has to know: a Pistol spent this turn unlocks a secret question, and the panel that offers it can
/// only be drawn if the view says so.
/// </summary>
public class ProjectAbilitiesTests
{
    [Fact]
    public void Should_arm_the_pistol_for_every_viewer_when_the_seat_has_just_spent_it()
    {
        // Arrange
        // Projected truthfully for everyone rather than only for the holder. Spending an ability
        // writes a public history entry stamped with the same turn, so nothing here is new to the
        // table -- and a bool reading false for a seat that is armed would be a lie rather than a
        // redaction.
        GameState state = TestGame.InProgress();
        int asker = state.ActiveSeat!.Value;
        state = TestGame.Unwrap(GameEngine.SpendAbility(state, asker, AbilityType.Pistol));

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view => Assert.True(view.Seats[asker].PistolArmed));
        Assert.All(views, view => Assert.DoesNotContain("Pistol", view.Seats[asker].AbilitiesRemaining));
    }

    [Fact]
    public void Should_disarm_the_pistol_when_the_turn_has_moved_on()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        int asker = state.ActiveSeat!.Value;
        state = TestGame.Unwrap(GameEngine.SpendAbility(state, asker, AbilityType.Pistol));

        // Act
        // Asking ends the turn, and the arming is good for that turn alone.
        state = TestGame.Unwrap(GameEngine.AskQuestion(
            state, asker, (asker + 1) % state.PlayerCount, new QueryRange(Direction.N, Direction.S), TestGame.Layout));

        PlayerView view = Sut.Project(state, asker);

        // Assert
        Assert.False(view.Seats[asker].PistolArmed);
        Assert.DoesNotContain("Pistol", view.Seats[asker].AbilitiesRemaining);
    }

    [Fact]
    public void Should_leave_the_pistol_unarmed_when_only_the_barrel_and_the_shovel_are_spent()
    {
        // Arrange
        // Barrel and Shovel act on physical dice this app does not model, so being spent is their
        // whole effect. Nothing about them may unlock a secret question.
        GameState state = TestGame.InProgress();
        int asker = state.ActiveSeat!.Value;

        // Act
        state = TestGame.Unwrap(GameEngine.SpendAbility(state, asker, AbilityType.Barrel));
        state = TestGame.Unwrap(GameEngine.SpendAbility(state, asker, AbilityType.Shovel));
        PlayerView view = Sut.Project(state, asker);

        // Assert
        Assert.False(view.Seats[asker].PistolArmed);
        Assert.Equal(["Pistol"], view.Seats[asker].AbilitiesRemaining);
    }

    [Fact]
    public void Should_leave_every_seat_unarmed_when_nobody_has_spent_a_pistol()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.All(view.Seats, seat => Assert.False(seat.PistolArmed));
        Assert.All(view.Seats, seat => Assert.Equal(["Barrel", "Shovel", "Pistol"], seat.AbilitiesRemaining));
    }
}
