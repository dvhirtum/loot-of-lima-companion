using LootOfLima.Contracts;

namespace LootOfLima.Server.Tests.Hubs.GameHub;

/// <summary>
/// The nudges that ride alongside state, saying what just changed.
/// </summary>
/// <remarks>
/// An event names a kind and at most a seat. It carries no range, no location and no hand, which
/// is what makes it the one thing in this server that is identical for every viewer — and it is
/// still sent per connection, because there is exactly one method that sends anything.
/// </remarks>
public class EventTests
{
    [Fact]
    public async Task Should_tell_the_table_what_happened_when_a_seat_is_claimed()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient watcher = await harness.JoinAsync();
        TestClient player = await harness.JoinAsync(harness.IssueToken("Ada"));

        // Act
        await harness.ActAsync(player, "ClaimSeat", 2);

        // Assert
        await watcher.WaitForEventsAsync(1);
        GameEventDto happened = Assert.Single(watcher.Events);
        Assert.Equal("SeatClaimed", happened.Kind);
        Assert.Equal(2, happened.SeatIndex);
    }

    [Fact]
    public async Task Should_say_nothing_of_the_question_itself_when_one_is_asked()
    {
        // Arrange
        // The event says a question was asked and by whom. What was asked, and what came back, is
        // the projection's business, and the projection is what decides who may read it.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);
        int before = players[1].EventCount;

        // Act
        await harness.ActAsync(players[0], "AskQuestion", new AskQuestionRequest(TargetSeat: 3, "N", "SE"));

        // Assert
        await players[1].WaitForEventsAsync(before + 1);
        GameEventDto happened = players[1].Events[^1];
        Assert.Equal("QuestionAsked", happened.Kind);
        Assert.Equal(0, happened.SeatIndex);
    }

    [Fact]
    public async Task Should_send_no_event_when_a_command_is_refused()
    {
        // Arrange
        // A rejected command changed nothing, so there is nothing to nudge anybody about — and
        // which move somebody just tried and failed is not the table's business either.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient watcher = await harness.JoinAsync();
        TestClient first = await harness.JoinAsync(harness.IssueToken("Ada"));
        await harness.ActAsync(first, "ClaimSeat", 0);

        TestClient second = await harness.JoinAsync(harness.IssueToken("Grace"));
        int before = watcher.EventCount;

        // Act
        await second.InvokeAsync("ClaimSeat", 0);

        // Assert
        // Counting the watcher straight away would prove nothing: an event that was never sent and
        // one still in flight look the same from here, and it would be arriving on a different
        // connection from the refusal. So a command that does owe an event follows, and the count
        // is taken once that one has landed. Events reach a connection in the order they were sent,
        // so a stray from the refusal would already be sitting in front of it.
        Assert.NotEmpty(second.Errors);
        await harness.ActAsync(second, "ClaimSeat", 1);
        await watcher.WaitForEventsAsync(before + 1);

        Assert.Equal(before + 1, watcher.EventCount);
        Assert.Equal("SeatClaimed", watcher.Events[^1].Kind);
    }
}
