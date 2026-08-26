using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

public class ProjectHandsTests
{
    [Fact]
    public void Should_show_the_viewer_their_own_hand_when_the_view_is_built()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 1);

        // Assert
        SeatView seat = view.Seats[1];
        Assert.Equal(state.SeatAt(1).Hand.Select(location => location.ToString()), seat.KnownHand);
        Assert.True(seat.HandIsComplete);
        Assert.True(seat.IsViewer);
    }

    [Fact]
    public void Should_never_show_a_whole_foreign_hand_when_the_view_is_built()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        foreach (SeatView seat in view.Seats.Where(seat => !seat.IsViewer))
        {
            Assert.False(seat.HandIsComplete);
            Assert.NotEqual(
                state.SeatAt(seat.Index).Hand.Select(location => location.ToString()),
                seat.KnownHand);
        }
    }

    [Fact]
    public void Should_show_a_foreign_hand_size_when_the_view_is_built()
    {
        // Arrange
        // Hand sizes never change, so they are plainly visible at the table and carry no secret.
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.All(view.Seats, seat => Assert.Equal(state.SeatAt(seat.Index).Hand.Count, seat.HandSize));
    }

    [Fact]
    public void Should_show_a_token_the_viewer_passed_when_the_view_is_built()
    {
        // Arrange
        // The whole point of setup passing: the giver knows one location a named opponent holds.
        GameState state = TestGame.InProgress();
        PassRecord pass = state.PassRecords.First(record => record.GiverSeat == 0);

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Contains(pass.Location.ToString(), view.Seats[pass.ReceiverSeat].KnownHand);
    }

    [Fact]
    public void Should_show_nothing_of_a_hand_the_viewer_never_passed_to_when_the_view_is_built()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        int stranger = Enumerable.Range(0, state.PlayerCount)
            .First(seat => seat != 0 && !state.PassRecords.Any(pass => pass.GiverSeat == 0 && pass.ReceiverSeat == seat));

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Empty(view.Seats[stranger].KnownHand);
    }

    [Fact]
    public void Should_show_no_hand_at_all_when_the_viewer_is_a_spectator()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: null);

        // Assert
        Assert.All(view.Seats, seat => Assert.Empty(seat.KnownHand));
        Assert.All(view.Seats, seat => Assert.False(seat.HandIsComplete));
        Assert.Null(view.ViewerSeat);
    }

    [Fact]
    public void Should_not_credit_the_viewer_with_knowing_a_hand_they_only_received_from()
    {
        // Arrange
        // A token you were given says what the giver used to hold, not what they hold now — and it
        // is already sitting in your own hand.
        GameState state = TestGame.InProgress();
        PassRecord received = state.PassRecords.First(pass => pass.ReceiverSeat == 0);

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.DoesNotContain(received.Location.ToString(), view.Seats[received.GiverSeat].KnownHand);
    }

    [Fact]
    public void Should_only_ever_name_locations_the_viewer_may_know_when_the_view_is_built()
    {
        // Arrange
        // The catch-all: for every seat, everything the view says about hands must come from the
        // viewer's own hand, from what they passed, or from what is face up on the table.
        GameState state = TestGame.InProgress();

        foreach (Seat viewer in state.Seats)
        {
            HashSet<string> allowed =
            [
                .. viewer.Hand.Select(location => location.ToString()),
                .. state.Deal!.PublicReveal.Select(location => location.ToString()),
                .. state.PassRecords
                    .Where(pass => pass.GiverSeat == viewer.Index)
                    .Select(pass => pass.Location.ToString()),
            ];

            // Act
            PlayerView view = Sut.Project(state, viewer.Index);

            // Assert
            Assert.All(
                view.Seats.SelectMany(seat => seat.KnownHand),
                location => Assert.Contains(location, allowed));
        }
    }
}
