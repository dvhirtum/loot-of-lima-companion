using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

public class ProjectTokenPassTests
{
    [Fact]
    public void Should_show_the_token_when_the_viewer_gave_it()
    {
        // Arrange
        // At five players a seat gives twice, once right and once left, so this compares sets.
        GameState state = TestGame.InProgress();
        List<string> given =
            [.. state.PassRecords.Where(pass => pass.GiverSeat == 0).Select(pass => pass.Location.ToString())];

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Equal(
            given.Order(),
            view.History.OfType<TokenPassView>()
                .Where(entry => entry.GiverSeat == 0)
                .Select(entry => entry.Location)
                .Order());
    }

    [Fact]
    public void Should_show_the_token_when_the_viewer_received_it()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        List<string> received =
            [.. state.PassRecords.Where(pass => pass.ReceiverSeat == 0).Select(pass => pass.Location.ToString())];

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Equal(
            received.Order(),
            view.History.OfType<TokenPassView>()
                .Where(entry => entry.ReceiverSeat == 0)
                .Select(entry => entry.Location)
                .Order());
    }

    [Fact]
    public void Should_redact_the_token_by_type_when_the_viewer_is_neither_endpoint()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        foreach (TokenPassOccurredView occurred in view.History.OfType<TokenPassOccurredView>())
        {
            Assert.NotEqual(0, occurred.GiverSeat);
            Assert.NotEqual(0, occurred.ReceiverSeat);
        }
    }

    [Fact]
    public void Should_still_show_who_passed_to_whom_when_the_viewer_is_neither_endpoint()
    {
        // Arrange
        // Which token moved is private; that it moved, and between whom, happens in plain sight.
        GameState state = TestGame.InProgress();
        PassRecord stranger = state.PassRecords.First(pass => pass.GiverSeat != 0 && pass.ReceiverSeat != 0);

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Contains(
            view.History.OfType<TokenPassOccurredView>(),
            occurred => occurred.GiverSeat == stranger.GiverSeat && occurred.ReceiverSeat == stranger.ReceiverSeat);
    }

    [Fact]
    public void Should_redact_every_pass_when_the_viewer_is_a_spectator()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: null);

        // Assert
        Assert.Empty(view.History.OfType<TokenPassView>());
        Assert.Equal(state.PassRecords.Count(), view.History.OfType<TokenPassOccurredView>().Count());
    }

    [Fact]
    public void Should_project_no_passes_at_all_when_the_deal_came_from_a_printed_scenario()
    {
        // Arrange
        GameState state = TestGame.InProgress(kind: DealSourceKind.Scenario);

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Empty(view.History.OfType<TokenPassView>());
        Assert.Empty(view.History.OfType<TokenPassOccurredView>());
    }
}
