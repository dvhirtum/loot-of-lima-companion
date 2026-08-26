using System.Text.Json;
using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Projection.Projector;

namespace LootOfLima.Projection.Tests.Projector;

/// <summary>
/// Leak tests over the serialized bytes, not over the object graph.
/// </summary>
/// <remarks>
/// Substring matching on raw JSON is deliberately crude, and that is exactly why it is worth
/// having: it does not care how the view is shaped, only about what actually goes out on the wire.
/// A redaction that is defeated by a new property, a serializer setting or a future refactor still
/// has to get past a plain string search. M3 will run the same shape of assertion over real hub
/// frames.
/// </remarks>
public class SerializedViewTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string Serialize(GameState state, int? viewerSeat) =>
        JsonSerializer.Serialize(Sut.Project(state, viewerSeat), Options);

    [Fact]
    public void Should_write_no_treasure_location_in_any_frame_when_the_game_is_under_way()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        List<string> treasure = [.. state.Deal!.Treasure.Select(location => location.ToString())];

        // Act
        List<string> frames = [.. state.Seats.Select(seat => Serialize(state, seat.Index)), Serialize(state, null)];

        // Assert
        foreach (string frame in frames)
        {
            Assert.All(treasure, location => Assert.DoesNotContain($"\"{location}\"", frame, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Should_write_only_locations_the_viewer_may_know_when_a_frame_is_serialized()
    {
        // Arrange
        GameState state = TestGame.InProgress();

        foreach (Seat viewer in state.Seats)
        {
            HashSet<string> allowed =
            [
                .. viewer.Hand.Select(location => location.ToString()),
                .. state.Deal!.PublicReveal.Select(location => location.ToString()),
                .. state.PassRecords
                    .Where(pass => pass.GiverSeat == viewer.Index || pass.ReceiverSeat == viewer.Index)
                    .Select(pass => pass.Location.ToString()),
            ];

            // Act
            string frame = Serialize(state, viewer.Index);

            // Assert
            foreach (LocationId location in LocationId.All.Where(l => !allowed.Contains(l.ToString())))
            {
                Assert.DoesNotContain($"\"{location}\"", frame, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Should_write_no_range_or_answer_for_a_secret_question_when_the_viewer_took_no_part()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        state = TestGame.Unwrap(GameEngine.SpendAbility(state, 0, AbilityType.Pistol));
        state = TestGame.Unwrap(GameEngine.AskSecretQuestion(
            state, 0, 2, new QueryRange(Direction.NE, Direction.SW), TestGame.Layout));

        // Act
        string outsider = Serialize(state, 1);
        string participant = Serialize(state, 0);

        // Assert
        Assert.Contains("secretQuestionOccurred", outsider, StringComparison.Ordinal);
        Assert.DoesNotContain("\"start\"", outsider, StringComparison.Ordinal);
        Assert.DoesNotContain("\"answer\"", outsider, StringComparison.Ordinal);

        Assert.Contains("\"secretQuestion\"", participant, StringComparison.Ordinal);
        Assert.Contains("\"start\"", participant, StringComparison.Ordinal);
        Assert.Contains("\"answer\"", participant, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_write_no_named_pair_for_a_failed_accusation_when_the_viewer_did_not_make_it()
    {
        // Arrange
        GameState state = TestGame.InProgress();
        LocationId first = TestGame.NotTreasure(state);
        LocationId second = TestGame.NotTreasure(state, skip: 1);
        state = TestGame.Unwrap(GameEngine.Accuse(state, 1, first, second));

        // Act
        string outsider = Serialize(state, 0);

        // Assert
        Assert.Contains("accusationOccurred", outsider, StringComparison.Ordinal);
        Assert.DoesNotContain("\"named\"", outsider, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_write_the_treasure_when_the_game_is_over()
    {
        // Arrange
        // The counterweight to the other tests here: they would all pass on an empty view, so this
        // one proves the treasure really does travel once it is allowed to.
        GameState state = TestGame.Finished();

        // Act
        string frame = Serialize(state, 0);

        // Assert
        Assert.All(
            state.Deal!.Treasure,
            location => Assert.Contains($"\"{location}\"", frame, StringComparison.Ordinal));
    }
}
