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

    [Fact]
    public async Task Should_tell_nobody_else_when_a_client_arrives()
    {
        // Arrange
        // An arrival changes no seat, no hand and no history, so the table has nothing to redraw.
        // It also read the game outside the store's gate, which is the part that could do harm: a
        // reconnect landing mid-command would push that older snapshot over everybody's view.
        //
        // A frame that was never sent and a frame that has not arrived yet look identical, so this
        // does not count frames straight after the arrival. It acts afterwards and waits for the
        // frame that action owes, which SignalR delivers in order down the same connection: had
        // the arrival sent one too, it would already be sitting in front of this one.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient sitting = await harness.JoinAsync(harness.IssueToken("Ada"));
        int framesBefore = sitting.StateFrames.Count;

        // Act
        await harness.JoinAsync();

        // Assert
        await harness.ActAsync(sitting, "ClaimSeat", 0);
        Assert.Equal(framesBefore + 1, sitting.StateFrames.Count);
    }

    [Fact]
    public async Task Should_send_the_version_the_game_is_at_when_a_client_arrives()
    {
        // Arrange
        // Arriving late still has to place the frame in the sequence, or the first thing a client
        // does with a later frame is decide it is older than what it already has.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient first = await harness.JoinAsync(harness.IssueToken("Ada"));
        await harness.ActAsync(first, "ClaimSeat", 0);

        // Act
        TestClient late = await harness.JoinAsync();

        // Assert
        Assert.Equal((await harness.StoredAsync()).Version, late.View.Version);
    }

    [Fact]
    public async Task Should_send_nothing_to_a_connection_that_never_said_which_game_it_wants()
    {
        // Arrange
        // A connected client that has not rejoined is on no game's list. It is owed nothing when
        // anybody acts, and the harness has to know that: waiting for a frame the server will
        // never send is ten seconds of hanging and then a message blaming the server for it.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient player = await harness.JoinAsync(harness.IssueToken("Ada"));
        TestClient stranger = await harness.ConnectAsync();

        // Act
        await harness.ActAsync(player, "ClaimSeat", 0);

        // Assert
        Assert.Empty(stranger.StateFrames);
        Assert.NotEmpty(player.StateFrames);
    }
}
