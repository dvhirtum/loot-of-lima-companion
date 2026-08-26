using LootOfLima.Contracts;
using LootOfLima.Rules;

namespace LootOfLima.Server.Tests;

/// <summary>
/// A game played end to end over a real hub. Not a unit test of anything, which is why it sits at
/// the root rather than in a per-method folder.
/// </summary>
public class PlayingAFullGameTests
{
    [Fact]
    public async Task Should_reach_play_when_two_players_sit_down_and_start()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();

        // Act
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Assert
        PlayerView view = players[0].View;
        Assert.Equal("InProgress", view.Phase);
        Assert.Equal(1, view.TurnNumber);
        Assert.All(view.Seats, seat => Assert.NotNull(seat.FewestTerrain));
        Assert.All(players, player => Assert.Empty(player.Errors));
    }

    [Fact]
    public async Task Should_answer_the_question_and_pass_the_turn_when_a_player_asks()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await harness.ActAsync(players[0], "AskQuestion", new AskQuestionRequest(TargetSeat: 3, "N", "SE"));

        // Assert
        PublicQuestionView asked = Assert.IsType<PublicQuestionView>(
            Assert.Single(players[0].View.History.OfType<PublicQuestionView>()));

        Assert.Equal(0, asked.AskerSeat);
        Assert.Equal(3, asked.TargetSeat);
        Assert.InRange(asked.Answer, 0, 4);
        Assert.Equal(1, players[0].View.ActiveSeat);
    }

    [Fact]
    public async Task Should_show_the_question_to_everybody_when_it_is_a_public_one()
    {
        // Arrange
        // A dice question is asked aloud and answered aloud, so it is public in full.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);
        TestClient watcher = await harness.JoinAsync();

        // Act
        await harness.ActAsync(players[0], "AskQuestion", new AskQuestionRequest(TargetSeat: 3, "N", "SE"));

        // Assert
        Assert.All(
            new[] { players[0].View, players[1].View, watcher.View },
            view => Assert.Single(view.History.OfType<PublicQuestionView>()));
    }

    [Fact]
    public async Task Should_end_the_game_when_somebody_names_the_treasure()
    {
        // Arrange
        // The accusation is correct on purpose: the treasure is read from the server's own state,
        // which is the one thing a test may do and a client may not.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        GameState state = await harness.CurrentAsync();
        IReadOnlyList<LocationId> treasure = state.Deal!.Treasure;

        // Act
        await harness.ActAsync(
            players[0], "Accuse", new AccuseRequest(treasure[0].ToString(), treasure[1].ToString()));

        // Assert
        PlayerView view = players[0].View;
        Assert.Equal("Finished", view.Phase);
        Assert.Equal(0, view.WinnerSeat);
        Assert.Equal("CorrectAccusation", view.EndReason);
    }

    [Fact]
    public async Task Should_keep_the_treasure_hidden_until_the_game_is_over()
    {
        // Arrange
        // The one field the whole design exists to protect. Null in every phase but Finished.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        Assert.Null(players[0].View.Treasure);

        GameState state = await harness.CurrentAsync();
        IReadOnlyList<LocationId> treasure = state.Deal!.Treasure;

        // Act
        await harness.ActAsync(
            players[0], "Accuse", new AccuseRequest(treasure[0].ToString(), treasure[1].ToString()));

        // Assert
        Assert.Equal(2, players[0].View.Treasure?.Count);
    }
}
