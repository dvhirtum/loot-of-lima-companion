using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

public class ProjectSecretQuestionTests
{
    /// <summary>Seat 0 spends its Pistol and asks seat 2. Seats 1, 3 and 4 are not party to it.</summary>
    private static GameState AfterPistol()
    {
        GameState state = TestGame.InProgress();
        state = TestGame.Unwrap(GameEngine.SpendAbility(state, 0, AbilityType.Pistol));
        return TestGame.Unwrap(GameEngine.AskSecretQuestion(
            state, 0, 2, new QueryRange(Direction.NE, Direction.SW), TestGame.Layout));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Should_show_the_question_in_full_when_the_viewer_took_part(int viewerSeat)
    {
        // Arrange
        GameState state = AfterPistol();
        SecretQuestionEntry entry = state.History.OfType<SecretQuestionEntry>().Single();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat);

        // Assert
        SecretQuestionView projected = Assert.Single(view.History.OfType<SecretQuestionView>());
        Assert.Equal(entry.Answer, projected.Answer);
        Assert.Equal("NE", projected.Start);
        Assert.Equal("SW", projected.End);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void Should_redact_the_question_by_type_when_the_viewer_took_no_part(int viewerSeat)
    {
        // Arrange
        // Redaction is by type, not by nulling fields: the type a non-participant receives has no
        // property that could carry the range or the answer, so there is nothing left to leak.
        GameState state = AfterPistol();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat);

        // Assert
        Assert.Empty(view.History.OfType<SecretQuestionView>());
        SecretQuestionOccurredView occurred = Assert.Single(view.History.OfType<SecretQuestionOccurredView>());
        Assert.Equal(0, occurred.AskerSeat);
        Assert.Equal(2, occurred.TargetSeat);
    }

    [Fact]
    public void Should_redact_the_question_when_the_viewer_is_a_spectator()
    {
        // Arrange
        GameState state = AfterPistol();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: null);

        // Assert
        Assert.Single(view.History.OfType<SecretQuestionOccurredView>());
        Assert.Empty(view.History.OfType<SecretQuestionView>());
    }

    [Fact]
    public void Should_still_show_that_a_pistol_was_spent_when_the_viewer_took_no_part()
    {
        // Arrange
        // The ability token visibly leaves the player's board, so spending it is never a secret.
        GameState state = AfterPistol();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 3);

        // Assert
        AbilityUsedView used = Assert.Single(view.History.OfType<AbilityUsedView>());
        Assert.Equal("Pistol", used.Ability);
        Assert.DoesNotContain("Pistol", view.Seats[0].AbilitiesRemaining);
    }

    [Fact]
    public void Should_keep_the_history_in_step_for_everyone_when_a_question_is_redacted()
    {
        // Arrange
        // Everybody sees the same number of entries in the same order; only their contents differ.
        GameState state = AfterPistol();

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view => Assert.Equal(state.History.Count, view.History.Count));
        Assert.All(
            views,
            view => Assert.Equal(state.History.Select(entry => entry.Sequence), view.History.Select(entry => entry.Sequence)));
    }
}
