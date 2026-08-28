using LootOfLima.Contracts;
using LootOfLima.Rules;

namespace LootOfLima.Server.Tests.Hubs.GameHub;

/// <summary>
/// The one way out of a solo game that is not a correct guess.
/// </summary>
/// <remarks>
/// Every condition on it is the engine's, so these check that the hub carries them faithfully:
/// that the refusal reaches the caller alone, and that the one command in this server whose whole
/// job is to reveal the treasure does so only when it is entitled to.
/// </remarks>
public class GiveUpTests
{
    [Fact]
    public async Task Should_finish_the_game_and_reveal_the_treasure_when_the_only_player_gives_up()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness, humans: 1);

        GameState state = await harness.CurrentAsync();
        string[] treasure = [.. state.Deal!.Treasure.Select(location => location.ToString())];
        await harness.ActAsync(players[0], "Accuse", HubGame.WrongPair(state));

        // Act
        await harness.ActAsync(players[0], "GiveUp");

        // Assert
        PlayerView view = players[0].View;
        Assert.Equal("Finished", view.Phase);
        Assert.Equal("Conceded", view.EndReason);
        Assert.Null(view.WinnerSeat);
        Assert.Equal(treasure.Order(), view.Treasure!.Order());
    }

    [Fact]
    public async Task Should_tell_a_watching_spectator_too_when_the_only_player_gives_up()
    {
        // Arrange
        // Finished is Finished for everybody. The treasure stops being a secret at the same instant
        // for the player who asked and for anybody looking over their shoulder.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness, humans: 1);
        TestClient watcher = await harness.JoinAsync();

        await harness.ActAsync(players[0], "Accuse", HubGame.WrongPair(await harness.CurrentAsync()));

        // Act
        await harness.ActAsync(players[0], "GiveUp");

        // Assert
        Assert.Equal("Finished", watcher.View.Phase);
        Assert.Equal("Conceded", watcher.View.EndReason);
        Assert.NotNull(watcher.View.Treasure);
    }

    [Fact]
    public async Task Should_refuse_and_reveal_nothing_when_the_player_has_not_yet_accused()
    {
        // Arrange
        // The refusal is what stops this being a "show me the answer" button. Without a wrong
        // accusation behind it there is no giving up, only spoiling.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness, humans: 1);

        // Act
        await harness.ActAsync(players[0], "GiveUp");

        // Assert
        Assert.Equal(GameErrorCode.NothingToGiveUpOn.ToString(), Assert.Single(players[0].Errors).Code);
        Assert.Null(players[0].View.Treasure);
        Assert.Equal(GamePhase.InProgress, (await harness.CurrentAsync()).Phase);
    }

    [Fact]
    public async Task Should_refuse_when_a_second_human_is_at_the_table()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness, humans: 3);

        await harness.ActAsync(players[0], "Accuse", HubGame.WrongPair(await harness.CurrentAsync()));

        // Act
        await harness.ActAsync(players[1], "GiveUp");

        // Assert
        Assert.Equal(GameErrorCode.NotASoloGame.ToString(), Assert.Single(players[1].Errors).Code);
        Assert.Null(players[1].View.Treasure);
    }

    [Fact]
    public async Task Should_refuse_and_tell_nobody_else_when_a_spectator_gives_up()
    {
        // Arrange
        // A spectator has no seat to concede from, and which commands somebody just tried and
        // failed is not the table's business.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness, humans: 1);
        TestClient watcher = await harness.JoinAsync();

        await harness.ActAsync(players[0], "Accuse", HubGame.WrongPair(await harness.CurrentAsync()));

        // Act
        await watcher.InvokeAsync("GiveUp");

        // Assert
        Assert.Equal("NotAPlayer", Assert.Single(watcher.Errors).Code);
        Assert.Empty(players[0].Errors);
        Assert.Null(watcher.View.Treasure);
    }
}
