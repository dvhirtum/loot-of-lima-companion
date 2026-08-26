using LootOfLima.Contracts;

namespace LootOfLima.Server.Tests.Hubs.GameHub;

/// <summary>
/// The revision number every frame carries.
/// </summary>
/// <remarks>
/// Commands are ordered by the store's gate; the sends that follow them are not, because each is a
/// separate await over a separate connection. Nothing here tries to stop a frame overtaking its
/// successor — the stamp is what lets the client on the other end notice that one did.
/// </remarks>
public class VersionStampTests
{
    [Fact]
    public async Task Should_rise_by_one_when_a_command_is_applied()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient player = await harness.JoinAsync(harness.IssueToken("Ada"));
        long before = (await harness.StoredAsync()).Version;

        // Act
        await harness.ActAsync(player, "ClaimSeat", 0);

        // Assert
        Assert.Equal(before + 1, (await harness.StoredAsync()).Version);
    }

    [Fact]
    public async Task Should_not_rise_when_a_command_is_refused()
    {
        // Arrange
        // A rejected command stored nothing, so there is no new revision for anybody to have missed.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient first = await harness.JoinAsync(harness.IssueToken("Ada"));
        await harness.ActAsync(first, "ClaimSeat", 0);

        TestClient second = await harness.JoinAsync(harness.IssueToken("Grace"));
        long before = (await harness.StoredAsync()).Version;

        // Act
        await second.InvokeAsync("ClaimSeat", 0);

        // Assert
        Assert.NotEmpty(second.Errors);
        Assert.Equal(before, (await harness.StoredAsync()).Version);
    }

    [Fact]
    public async Task Should_reach_every_client_with_the_same_number_when_a_command_is_applied()
    {
        // Arrange
        // One command produced one revision, so the frames it caused all describe that revision —
        // even though each viewer is sent a different projection of it.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient player = await harness.JoinAsync(harness.IssueToken("Ada"));
        TestClient watcher = await harness.JoinAsync();

        // Act
        await harness.ActAsync(player, "ClaimSeat", 0);

        // Assert
        long stored = (await harness.StoredAsync()).Version;
        Assert.Equal(stored, player.View.Version);
        Assert.Equal(stored, watcher.View.Version);
    }

    [Fact]
    public async Task Should_climb_with_every_frame_when_a_game_is_played_out()
    {
        // Arrange
        // Read from the frames a client actually received rather than from the store, because the
        // property is that a client can order what it is sent, not that the server counted right.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        long[] versions =
        [
            .. players[0].StateFrames.Select(
                frame => System.Text.Json.JsonSerializer.Deserialize<PlayerView>(
                    frame, new System.Text.Json.JsonSerializerOptions(
                        System.Text.Json.JsonSerializerDefaults.Web))!.Version),
        ];

        // Assert
        Assert.NotEmpty(versions);
        Assert.Equal(versions.Order(), versions);
        Assert.Equal(versions.Distinct().Count(), versions.Length);
    }
}
