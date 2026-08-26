using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

public class ProjectAccusationTests
{
    /// <summary>Seat 1 guesses wrong and is eliminated; seats 0 and 2 play on.</summary>
    private static GameState AfterWrongAccusation()
    {
        GameState state = TestGame.InProgress();
        return TestGame.Unwrap(
            GameEngine.Accuse(state, 1, TestGame.NotTreasure(state), TestGame.NotTreasure(state, skip: 1)));
    }

    [Fact]
    public void Should_show_the_named_pair_when_the_viewer_made_the_wrong_accusation()
    {
        // Arrange
        GameState state = AfterWrongAccusation();
        AccusationEntry entry = state.History.OfType<AccusationEntry>().Single();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 1);

        // Assert
        AccusationView projected = Assert.Single(view.History.OfType<AccusationView>());
        Assert.Equal(entry.Named.Select(location => location.ToString()), projected.Named);
        Assert.False(projected.Correct);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Should_redact_the_named_pair_by_type_when_the_accusation_was_wrong(int viewerSeat)
    {
        // Arrange
        // The rulebook says a failed accusation reveals nothing, and a pair ruled out is a real
        // deduction. So the type everybody else receives has no property for the pair at all.
        GameState state = AfterWrongAccusation();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat);

        // Assert
        Assert.Empty(view.History.OfType<AccusationView>());
        AccusationOccurredView occurred = Assert.Single(view.History.OfType<AccusationOccurredView>());
        Assert.Equal(1, occurred.AccuserSeat);
    }

    [Fact]
    public void Should_still_show_the_elimination_when_the_accusation_was_wrong()
    {
        // Arrange
        // Announcing the failure is part of the rule; it is only the pair that stays secret.
        GameState state = AfterWrongAccusation();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.True(view.Seats[1].Eliminated);
    }

    [Fact]
    public void Should_show_the_named_pair_to_everybody_when_the_accusation_was_right()
    {
        // Arrange
        // A correct accusation names the treasure and ends the game, so there is nothing left to hide.
        GameState state = TestGame.Finished();

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view => Assert.True(Assert.Single(view.History.OfType<AccusationView>()).Correct));
    }

    [Fact]
    public void Should_show_the_ending_to_everybody_when_the_game_finishes()
    {
        // Arrange
        GameState state = TestGame.Finished();

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view => Assert.Equal(0, view.WinnerSeat));
        Assert.All(views, view => Assert.Equal("CorrectAccusation", view.EndReason));
        Assert.All(views, view => Assert.Equal("Finished", view.Phase));
    }
}
