using System.Text.Json;
using System.Text.RegularExpressions;
using LootOfLima.Contracts;
using LootOfLima.Rules;

namespace LootOfLima.Server.Tests;

/// <summary>
/// What every client was actually sent, byte for byte.
/// </summary>
/// <remarks>
/// <para>
/// These read raw JSON and match substrings. That is crude on purpose. Every other guard in this
/// server is a statement about code — a type that cannot be named, a function that is the only
/// caller, a resolver that refuses. This one is a statement about bytes, and it holds no matter
/// which of those guards is wrong, removed, or quietly worked around. A leak that survives a
/// substring search of everything a viewer received is a leak that is genuinely not there.
/// </para>
/// <para>
/// So there is nothing clever here, and there should never be: the value is precisely that it is
/// too dumb to be fooled by a mistake anywhere upstream.
/// </para>
/// </remarks>
public class LeakTests
{
    /// <summary>The 24 island locations as they are named on the wire: B1-B8, F1-F8, M1-M8.</summary>
    private static readonly Regex LocationName = new(@"\b[BFM][1-8]\b", RegexOptions.Compiled);

    [Fact]
    public async Task Should_send_no_location_a_viewer_is_not_entitled_to_when_a_game_is_played()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);
        TestClient watcher = await harness.JoinAsync();

        // Act
        await harness.ActAsync(players[0], "AskQuestion", new AskQuestionRequest(TargetSeat: 3, "N", "SE"));
        await harness.ActAsync(players[1], "AskQuestion", new AskQuestionRequest(TargetSeat: 0, "E", "W", "Beach"));

        // Assert
        GameState state = await harness.CurrentAsync();
        foreach ((TestClient client, int? seat) in new[]
        {
            (players[0], (int?)0),
            (players[1], 1),
            (watcher, null),
        })
        {
            IReadOnlySet<string> entitled = Entitled(state, seat);
            IReadOnlySet<string> sent = LocationsIn(client);

            Assert.True(
                sent.IsSubsetOf(entitled),
                $"Seat {seat?.ToString() ?? "spectator"} was sent {string.Join(", ", sent.Except(entitled))}, "
                + $"and is entitled only to {string.Join(", ", entitled)}.");
        }
    }

    [Fact]
    public async Task Should_send_the_treasure_to_nobody_at_all_while_the_game_is_unfinished()
    {
        // Arrange
        // The two locations the entire design exists to protect. Not redacted, not nulled, not
        // present in any frame in any form.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);
        TestClient watcher = await harness.JoinAsync();

        // Act
        await harness.ActAsync(players[0], "AskQuestion", new AskQuestionRequest(TargetSeat: 3, "N", "SE"));

        // Assert
        GameState state = await harness.CurrentAsync();
        string[] treasure = [.. state.Deal!.Treasure.Select(location => location.ToString())];

        foreach (TestClient client in new[] { players[0], players[1], watcher })
        {
            foreach (string secret in treasure)
            {
                Assert.DoesNotContain(secret, LocationsIn(client));
            }
        }
    }

    [Fact]
    public async Task Should_send_the_treasure_to_nobody_when_the_only_player_accuses_wrongly()
    {
        // Arrange
        // This is the guard the whole solo change hangs on. A wrong accusation used to eliminate
        // the only player, which finished the game, and Finished is the one phase that projects the
        // treasure -- so the punishment for guessing wrong was being handed the answer. Nothing
        // about that would show up as a rule failing; it would show up here, as two locations
        // arriving in a frame.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness, humans: 1);
        TestClient watcher = await harness.JoinAsync();

        GameState state = await harness.CurrentAsync();
        string[] treasure = [.. state.Deal!.Treasure.Select(location => location.ToString())];

        // Act
        await harness.ActAsync(players[0], "Accuse", HubGame.WrongPair(state));

        // Assert
        Assert.Equal(GamePhase.InProgress, (await harness.CurrentAsync()).Phase);
        foreach (TestClient client in new[] { players[0], watcher })
        {
            foreach (string secret in treasure)
            {
                Assert.DoesNotContain(secret, LocationsIn(client));
            }
        }
    }

    [Fact]
    public async Task Should_give_a_bystander_no_field_for_the_secret_when_a_pistol_question_is_asked()
    {
        // Arrange
        // Redaction by type, not by nulling: a bystander receives a different shape with nowhere
        // for the range or the answer to sit, so no forgotten branch or serializer setting can
        // put them back.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);
        TestClient watcher = await harness.JoinAsync();

        // Act
        await harness.ActAsync(players[0], "SpendAbility", "Pistol");
        await harness.ActAsync(players[0], "AskSecretQuestion", new AskSecretQuestionRequest(TargetSeat: 1, "N", "SE"));

        // Assert
        foreach (TestClient participant in new[] { players[0], players[1] })
        {
            JsonElement entry = SecretQuestionEntryIn(participant);
            Assert.Equal("secretQuestion", entry.GetProperty("type").GetString());
            Assert.True(entry.TryGetProperty("answer", out _));
            Assert.True(entry.TryGetProperty("start", out _));
        }

        JsonElement redacted = SecretQuestionEntryIn(watcher);
        Assert.Equal("secretQuestionOccurred", redacted.GetProperty("type").GetString());
        Assert.False(redacted.TryGetProperty("answer", out _));
        Assert.False(redacted.TryGetProperty("start", out _));
        Assert.False(redacted.TryGetProperty("end", out _));
    }

    [Fact]
    public async Task Should_show_a_spectator_no_hand_at_all_when_they_watch_a_game()
    {
        // Arrange
        // A spectator is entitled to strictly less than any player: the public reveal and nothing
        // else. This is the whole of what a null viewer seat means.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        await HubGame.PlayToInProgressAsync(harness);
        TestClient watcher = await harness.JoinAsync();

        // Act
        PlayerView view = watcher.View;

        // Assert
        Assert.All(view.Seats, seat => Assert.Empty(seat.KnownHand));
        Assert.All(view.Seats, seat => Assert.False(seat.HandIsComplete));
        Assert.Equal(LocationsIn(watcher), view.PublicReveal.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>Every location name appearing anywhere in anything this client was sent.</summary>
    private static IReadOnlySet<string> LocationsIn(TestClient client) =>
        client.StateFrames
            .SelectMany(frame => LocationName.Matches(frame).Select(match => match.Value))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// What one viewer may legitimately know: their own hand, the public reveal, and the tokens
    /// they handed to somebody else at setup. A spectator gets the public reveal alone.
    /// </summary>
    private static IReadOnlySet<string> Entitled(GameState state, int? viewerSeat)
    {
        HashSet<string> entitled = [.. state.Deal!.PublicReveal.Select(location => location.ToString())];
        if (viewerSeat is not { } seat)
        {
            return entitled;
        }

        entitled.UnionWith(state.SeatAt(seat).Hand.Select(location => location.ToString()));
        entitled.UnionWith(state.PassRecords
            .Where(pass => pass.GiverSeat == seat)
            .Select(pass => pass.Location.ToString()));

        return entitled;
    }

    /// <summary>The one secret-question history entry from this client's most recent frame.</summary>
    private static JsonElement SecretQuestionEntryIn(TestClient client) =>
        JsonDocument.Parse(client.StateFrames[^1]).RootElement
            .GetProperty("history")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("type").GetString()!.StartsWith(
                "secretQuestion", StringComparison.Ordinal));
}
