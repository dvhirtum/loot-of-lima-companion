using LootOfLima.Contracts;

namespace LootOfLima.Server.Tests.Hubs.GameHub;

public class RejoinTests
{
    [Fact]
    public async Task Should_send_the_game_when_a_spectator_rejoins()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();

        // Act
        TestClient watcher = await harness.JoinAsync();

        // Assert
        PlayerView view = watcher.View;
        Assert.Equal(harness.RoomCode, view.RoomCode);
        Assert.Equal("Lobby", view.Phase);
        Assert.Null(view.ViewerSeat);
        Assert.Empty(watcher.Errors);
    }

    [Fact]
    public async Task Should_refuse_the_code_when_no_game_holds_it()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient client = await harness.ConnectAsync();

        // Act
        await client.InvokeAsync("Rejoin", "ZZZZ", null);

        // Assert
        Assert.Equal("NoSuchGame", Assert.Single(client.Errors).Code);
        Assert.Empty(client.StateFrames);
    }

    [Fact]
    public async Task Should_refuse_the_token_when_it_was_issued_for_another_game()
    {
        // Arrange
        // Downgrading a wrong token to a spectator view would leave a player looking at a table
        // with no hand on it and nothing at all to explain why.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient client = await harness.ConnectAsync();

        // Act
        await client.InvokeAsync("Rejoin", harness.RoomCode, "a-token-from-nowhere");

        // Assert
        Assert.Equal("UnknownPlayerToken", Assert.Single(client.Errors).Code);
    }

    [Fact]
    public async Task Should_show_the_same_seat_to_both_when_one_player_joins_from_two_devices()
    {
        // Arrange
        // A player who picks up a second device is still one player in one seat. Both connections
        // are bound separately and both project to the same seat.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        string token = harness.IssueToken("Ada");

        TestClient phone = await harness.JoinAsync(token);
        await phone.InvokeAsync("ClaimSeat", 2);

        // Act
        TestClient laptop = await harness.JoinAsync(token);

        // Assert
        Assert.Equal(2, laptop.View.ViewerSeat);
        Assert.Equal(2, phone.View.ViewerSeat);
    }
}
