using System.Text.Json;
using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

/// <summary>
/// The parts of a view that carry no secret, plus the guards on the function itself. The redaction
/// rules live in a class per secret alongside this one.
/// </summary>
public class ProjectTests
{
    [Fact]
    public void Should_carry_the_public_facts_when_the_view_is_built()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Equal(state.GameId, view.GameId);
        Assert.Equal(state.RoomCode, view.RoomCode);
        Assert.Equal("InProgress", view.Phase);
        Assert.Equal(state.PlayerCount, view.PlayerCount);
        Assert.Equal(state.ActiveSeat, view.ActiveSeat);
        Assert.Equal(state.TurnNumber, view.TurnNumber);
        Assert.Equal(0, view.ViewerSeat);
    }

    [Fact]
    public void Should_show_the_face_up_locations_to_everybody_when_the_game_has_been_dealt()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        List<string> reveal = [.. state.Deal!.PublicReveal.Select(location => location.ToString())];

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view => Assert.Equal(reveal, view.PublicReveal));
    }

    [Fact]
    public void Should_show_no_face_up_locations_when_the_game_is_still_in_the_lobby()
    {
        // Arrange
        GameState state = TestGame.Lobby(5, humans: 3);

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 0);

        // Assert
        Assert.Empty(view.PublicReveal);
        Assert.Null(view.Treasure);
    }

    [Fact]
    public void Should_show_every_seat_remaining_abilities_when_the_view_is_built()
    {
        // Arrange
        // Ability tokens sit face up on the player boards, so they are public.
        GameState state = TestGame.InProgress();
        state = TestGame.Unwrap(GameEngine.SpendAbility(state, 0, AbilityType.Shovel));

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 2);

        // Assert
        Assert.Equal(["Barrel", "Pistol"], view.Seats[0].AbilitiesRemaining);
        Assert.Equal(["Barrel", "Shovel", "Pistol"], view.Seats[2].AbilitiesRemaining);
    }

    [Fact]
    public void Should_mark_the_seat_whose_turn_it_is_when_the_view_is_built()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 2);

        // Assert
        Assert.True(view.Seats[0].IsActive);
        Assert.All(view.Seats.Skip(1), seat => Assert.False(seat.IsActive));
    }

    [Fact]
    public void Should_show_a_dice_question_in_full_to_everybody_when_one_has_been_asked()
    {
        // Arrange
        // Ordinary questions are answered out loud at the table. Everything about them is public.
        GameState state = TestGame.InProgress();
        state = TestGame.Unwrap(GameEngine.AskQuestion(
            state, 0, 4, new QueryRange(Direction.N, Direction.S, Terrain.Beach), TestGame.Layout));
        int answer = state.History.OfType<PublicQuestionEntry>().Single().Answer;

        // Act
        List<PlayerView> views = [.. state.Seats.Select(seat => Sut.Project(state, seat.Index))];

        // Assert
        Assert.All(views, view =>
        {
            PublicQuestionView question = Assert.Single(view.History.OfType<PublicQuestionView>());
            Assert.Equal(answer, question.Answer);
            Assert.Equal("N", question.Start);
            Assert.Equal("S", question.End);
            Assert.Equal("Beach", question.Terrain);
        });
    }

    [Fact]
    public void Should_leave_the_terrain_unset_when_the_question_was_not_restricted()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        state = TestGame.Unwrap(GameEngine.AskQuestion(
            state, 0, 4, new QueryRange(Direction.N, Direction.S), TestGame.Layout));

        // Act
        PlayerView view = Sut.Project(state, viewerSeat: 1);

        // Assert
        Assert.Null(Assert.Single(view.History.OfType<PublicQuestionView>()).Terrain);
    }

    [Fact]
    public void Should_throw_when_the_viewer_sits_at_no_such_seat()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => Sut.Project(state, viewerSeat: 9));

        // Assert
        Assert.Equal("viewerSeat", exception.ParamName);
    }

    [Fact]
    public void Should_project_the_same_view_twice_when_nothing_has_changed()
    {
        // Arrange
        // The projection is pure, which is what makes it safe to run on every broadcast. Compared
        // as serialized text because a record holding lists compares those lists by reference, so
        // two structurally identical views are never Equal to one another.
        GameState state = TestGame.InProgress();

        // Act
        string first = JsonSerializer.Serialize(Sut.Project(state, viewerSeat: 0));
        string second = JsonSerializer.Serialize(Sut.Project(state, viewerSeat: 0));

        // Assert
        Assert.Equal(first, second);
    }
}
