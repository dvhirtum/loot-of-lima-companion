using LootOfLima.Contracts;
using LootOfLima.Rules;

namespace LootOfLima.Server.Tests;

/// <summary>
/// What the hub refuses. Spans several commands rather than testing one, so it sits at the root.
/// </summary>
/// <remarks>
/// Every rejection is checked to reach the caller and nobody else. What somebody just tried and
/// failed to do is information about them — who is impatient, who is confused, who is about to
/// accuse — and none of it is the table's business.
/// </remarks>
public class AuthorizationTests
{
    [Fact]
    public async Task Should_refuse_the_question_when_it_is_not_that_player_turn()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[1].InvokeAsync("AskQuestion", new AskQuestionRequest(TargetSeat: 3, "N", "SE"));

        // Assert
        Assert.Equal(GameErrorCode.NotYourTurn.ToString(), Assert.Single(players[1].Errors).Code);
        Assert.Empty(players[0].Errors);
    }

    [Fact]
    public async Task Should_refuse_the_question_when_a_player_targets_themselves()
    {
        // Arrange
        // Asking your own hand a question would answer it from tokens you are already holding.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync("AskQuestion", new AskQuestionRequest(TargetSeat: 0, "N", "SE"));

        // Assert
        Assert.Equal(GameErrorCode.CannotTargetYourself.ToString(), Assert.Single(players[0].Errors).Code);
    }

    [Fact]
    public async Task Should_refuse_the_accusation_when_a_spectator_makes_it()
    {
        // Arrange
        // A spectator holds no seat, so there is nobody for the engine to eliminate if it is wrong.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        await HubGame.PlayToInProgressAsync(harness);
        TestClient watcher = await harness.JoinAsync();

        GameState state = await harness.CurrentAsync();
        IReadOnlyList<LocationId> treasure = state.Deal!.Treasure;

        // Act
        await watcher.InvokeAsync("Accuse", new AccuseRequest(treasure[0].ToString(), treasure[1].ToString()));

        // Assert
        Assert.Equal("NotAPlayer", Assert.Single(watcher.Errors).Code);
        Assert.Equal(GamePhase.InProgress, (await harness.CurrentAsync()).Phase);
    }

    [Fact]
    public async Task Should_refuse_the_seat_when_somebody_is_already_in_it()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient first = await harness.JoinAsync(harness.IssueToken("Ada"));
        await first.InvokeAsync("ClaimSeat", 2);

        TestClient second = await harness.JoinAsync(harness.IssueToken("Grace"));

        // Act
        await second.InvokeAsync("ClaimSeat", 2);

        // Assert
        Assert.Equal(GameErrorCode.SeatAlreadyClaimed.ToString(), Assert.Single(second.Errors).Code);
    }

    [Fact]
    public async Task Should_refuse_the_second_seat_when_one_player_already_holds_one()
    {
        // Arrange
        // Two seats for one identity would be two full-hand views of the same game for one person.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        string token = harness.IssueToken("Ada");
        TestClient client = await harness.JoinAsync(token);
        await client.InvokeAsync("ClaimSeat", 0);

        // Act
        await client.InvokeAsync("ClaimSeat", 1);

        // Assert
        Assert.Equal(GameErrorCode.PlayerAlreadySeated.ToString(), Assert.Single(client.Errors).Code);
    }

    [Fact]
    public async Task Should_refuse_the_secret_question_when_no_pistol_was_spent_this_turn()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync("AskSecretQuestion", new AskSecretQuestionRequest(TargetSeat: 1, "N", "SE"));

        // Assert
        Assert.Equal(GameErrorCode.PistolNotSpentThisTurn.ToString(), Assert.Single(players[0].Errors).Code);
    }

    [Fact]
    public async Task Should_refuse_the_command_when_the_caller_never_rejoined()
    {
        // Arrange
        // A connection that has not said which game it is in has no identity and no game.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient stranger = await harness.ConnectAsync();

        // Act
        await stranger.InvokeAsync("ClaimSeat", 0);

        // Assert
        Assert.Equal("NotAPlayer", Assert.Single(stranger.Errors).Code);
    }

    [Theory]
    [InlineData("NNE")]
    [InlineData("up")]
    [InlineData("")]
    public async Task Should_refuse_the_question_when_the_direction_is_not_a_direction(string start)
    {
        // Arrange
        // Directions cross the wire as strings, because Contracts cannot see the rules engine. The
        // server parses them, so it also has to refuse them.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync("AskQuestion", new AskQuestionRequest(TargetSeat: 3, start, "SE"));

        // Assert
        Assert.Equal("BadRequest", Assert.Single(players[0].Errors).Code);
    }
}
