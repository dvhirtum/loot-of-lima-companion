using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

public class ProjectFewestTerrainTests
{
    [Fact]
    public void Should_show_every_seat_announcement_to_everybody_when_the_announcements_are_done()
    {
        // Arrange
        // The announcement is made aloud at the table, so it is public in full.
        GameState state = TestGame.InProgress();
        List<string?> announced = [.. state.Seats.Select(seat => seat.FewestTerrain?.ToString())];

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view => Assert.Equal(announced, view.Seats.Select(seat => seat.FewestTerrain)));
        Assert.All(announced, terrain => Assert.NotNull(terrain));
    }

    [Fact]
    public void Should_show_the_announcements_in_the_history_when_they_have_been_made()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 1);

        // Assert
        Assert.Equal(state.PlayerCount, view.History.OfType<FewestTerrainAnnouncedView>().Count());
    }

    [Fact]
    public void Should_show_no_announcement_when_a_seat_has_not_announced_yet()
    {
        // Arrange
        GameState state = TestGame.Announcing();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Null(view.Seats[0].FewestTerrain);
        Assert.Equal("Announcing", view.Phase);
        Assert.Equal(0, view.ActiveSeat);
    }

    [Fact]
    public void Should_say_nothing_about_whether_a_choice_broke_a_tie_when_the_view_is_built()
    {
        // Arrange
        // The rulebook says a player breaking a tie must not reveal there was one, so the view
        // must carry the terrain alone. Nothing here may let an outsider recover the hand counts
        // the choice was made from.
        GameState state = TestGame.InProgress();
        Seat tied = state.Seats.First(seat => FewestTerrainRule.Minima(seat.Hand).Count > 1);

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: tied.Index == 0 ? 1 : 0);
        SeatView seen = view.Seats[tied.Index];

        // Assert
        Assert.Equal(tied.FewestTerrain!.Value.ToString(), seen.FewestTerrain);
        Assert.Empty(seen.KnownHand.Except(view.PublicReveal));
        Assert.False(seen.HandIsComplete);
    }
}
