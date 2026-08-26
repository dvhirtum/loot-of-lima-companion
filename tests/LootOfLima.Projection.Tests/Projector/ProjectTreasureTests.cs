using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

/// <summary>
/// The treasure is the one secret the whole game is played to find, so it gets a test class of its
/// own rather than a line inside a larger one.
/// </summary>
public class ProjectTreasureTests
{
    [Theory]
    [InlineData(GamePhase.Lobby)]
    [InlineData(GamePhase.Dealing)]
    [InlineData(GamePhase.Passing)]
    [InlineData(GamePhase.InProgress)]
    public void Should_withhold_the_treasure_when_the_game_is_not_over(GamePhase phase)
    {
        // Arrange
        GameState state = TestGame.InProgress() with { Phase = phase };

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Null(view.Treasure);
    }

    [Fact]
    public void Should_withhold_the_treasure_from_every_seat_when_the_game_is_under_way()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view => Assert.Null(view.Treasure));
    }

    [Fact]
    public void Should_reveal_the_treasure_when_the_game_is_over()
    {
        // Arrange
        GameState state = TestGame.Finished();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Equal(
            state.Deal!.Treasure.Select(location => location.ToString()),
            view.Treasure);
    }

    [Fact]
    public void Should_reveal_the_treasure_to_a_spectator_too_when_the_game_is_over()
    {
        // Arrange
        GameState state = TestGame.Finished();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: null);

        // Assert
        Assert.NotNull(view.Treasure);
    }

    [Fact]
    public void Should_never_name_a_treasure_location_anywhere_in_the_view_when_the_game_is_under_way()
    {
        // Arrange
        // Deliberately crude: search every string the view carries, not just the treasure field.
        // A leak that routes around the field this test is named for still has to survive this.
        GameState state = TestGame.InProgress();
        List<string> treasure = [.. state.Deal!.Treasure.Select(location => location.ToString())];

        // Act
        List<string> everything = [.. state.Seats.SelectMany(seat => Strings(Sut.Project(state, seat.Index)))];

        // Assert
        Assert.All(treasure, location => Assert.DoesNotContain(location, everything));
    }

    /// <summary>Every location-shaped string the view carries, from every corner of it.</summary>
    private static IEnumerable<string> Strings(PlayerView view) =>
    [
        .. view.PublicReveal,
        .. view.Treasure ?? [],
        .. view.Seats.SelectMany(seat => seat.KnownHand),
        .. view.History.SelectMany(entry => entry switch
        {
            TokenPassView pass => (IEnumerable<string>)[pass.Location],
            AccusationView accusation => accusation.Named,
            _ => [],
        }),
    ];
}
