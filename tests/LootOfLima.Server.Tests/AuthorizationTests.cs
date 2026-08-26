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

    [Theory]
    [InlineData("8")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("99")]
    public async Task Should_refuse_the_question_when_the_direction_is_written_as_a_number(string start)
    {
        // Arrange
        // Enum.TryParse also accepts the underlying number, defined or not, so "8" would arrive as a
        // ninth compass direction and index off the end of the arc table. "0" is the quieter half of
        // the same hole: it parses to North, but the wire contract is names, and one thing with two
        // spellings on it is a second way for a client and this server to disagree.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync("AskQuestion", new AskQuestionRequest(TargetSeat: 3, start, "SE"));

        // Assert
        Assert.Equal("BadRequest", Assert.Single(players[0].Errors).Code);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("sand")]
    public async Task Should_refuse_the_question_when_the_terrain_filter_is_not_a_terrain(string terrain)
    {
        // Arrange
        // A question's terrain is written into public history, so a filter nobody can name would be
        // shown to the whole table as whatever the number happened to print as.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync(
            "AskQuestion", new AskQuestionRequest(TargetSeat: 3, "N", "SE", terrain));

        // Assert
        Assert.Equal("BadRequest", Assert.Single(players[0].Errors).Code);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("swamp")]
    public async Task Should_refuse_the_announcement_when_the_terrain_is_not_a_terrain(string terrain)
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync("AnnounceFewestTerrain", terrain);

        // Assert
        Assert.Equal("BadRequest", Assert.Single(players[0].Errors).Code);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("0")]
    [InlineData("cutlass")]
    public async Task Should_refuse_the_ability_when_it_is_not_an_ability(string ability)
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync("SpendAbility", ability);

        // Assert
        Assert.Equal("BadRequest", Assert.Single(players[0].Errors).Code);
    }

    [Fact]
    public async Task Should_refuse_the_start_when_a_spectator_sends_it()
    {
        // Arrange
        // A spectator is the host's screen or somebody watching over a shoulder. Dealing the game
        // and choosing the scenario belong to the people playing it.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient player = await harness.JoinAsync(harness.IssueToken("Ada"));
        await harness.ActAsync(player, "ClaimSeat", 0);

        TestClient watcher = await harness.JoinAsync();

        // Act
        await watcher.InvokeAsync("StartGame", new StartGameRequest(null));

        // Assert
        // A spectator holds no identity at all, so it is refused a step before the seat is looked
        // for. That is the earlier and better answer of the two, and it is still a refusal.
        Assert.Equal("NotAPlayer", Assert.Single(watcher.Errors).Code);
        Assert.Equal(GamePhase.Lobby, (await harness.CurrentAsync()).Phase);
    }

    [Fact]
    public async Task Should_refuse_the_start_when_the_sender_joined_but_took_no_seat()
    {
        // Arrange
        // The other half of the same rule: this one is a player, with a token and a name, and is
        // still only standing next to the table.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient seated = await harness.JoinAsync(harness.IssueToken("Ada"));
        await harness.ActAsync(seated, "ClaimSeat", 0);

        TestClient standing = await harness.JoinAsync(harness.IssueToken("Grace"));

        // Act
        await standing.InvokeAsync("StartGame", new StartGameRequest(null));

        // Assert
        Assert.Equal(GameErrorCode.SeatNotClaimed.ToString(), Assert.Single(standing.Errors).Code);
        Assert.Equal(GamePhase.Lobby, (await harness.CurrentAsync()).Phase);
    }

    [Fact]
    public async Task Should_refuse_the_start_when_there_is_no_such_scenario()
    {
        // Arrange
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient player = await harness.JoinAsync(harness.IssueToken("Ada"));
        await harness.ActAsync(player, "ClaimSeat", 0);

        // Act
        await player.InvokeAsync("StartGame", new StartGameRequest("b99s99"));

        // Assert
        Assert.Equal("NoSuchScenario", Assert.Single(player.Errors).Code);
        Assert.Equal(GamePhase.Lobby, (await harness.CurrentAsync()).Phase);
    }

    [Fact]
    public async Task Should_start_the_scenario_when_its_id_arrives_in_the_wrong_case()
    {
        // Arrange
        // The ids are lower case and machine-supplied, but the check that accepts one and the
        // lookup that fetches it have to agree about case, or an id passes the guard and then
        // throws from the library instead of coming back as an error a client can read.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient player = await harness.JoinAsync(harness.IssueToken("Ada"));
        await harness.ActAsync(player, "ClaimSeat", 0);

        // Act
        await player.InvokeAsync("StartGame", new StartGameRequest("B1S1"));

        // Assert
        Assert.Empty(player.Errors);
        Assert.Equal(DealSourceKind.Scenario, (await harness.CurrentAsync()).Deal!.Source.Kind);
    }

    [Theory]
    [InlineData("StartGame")]
    [InlineData("AskQuestion")]
    [InlineData("AskSecretQuestion")]
    [InlineData("Accuse")]
    public async Task Should_refuse_the_command_when_its_request_is_missing(string method)
    {
        // Arrange
        // Every one of these arrives from a client, so a missing body is a bad request rather than
        // a bug in the server. Thrown, it would reach the caller as a HubException reading "An
        // unexpected error occurred" — untrue, no use to whoever has to fix their client, and the
        // one input in the whole hub that does not come back shaped like every other refusal.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        IReadOnlyList<TestClient> players = await HubGame.PlayToInProgressAsync(harness);

        // Act
        await players[0].InvokeAsync(method, [null]);

        // Assert
        Assert.Equal("BadRequest", Assert.Single(players[0].Errors).Code);
    }

    [Fact]
    public async Task Should_refuse_the_rejoin_when_the_room_code_is_missing()
    {
        // Arrange
        // A null code reaches a dictionary lookup, which throws on a null key rather than missing.
        await using HubHarness harness = new();
        await harness.SeedLobbyAsync();
        TestClient stranger = await harness.ConnectAsync();

        // Act
        await stranger.InvokeAsync("Rejoin", null, null);

        // Assert
        Assert.Equal("BadRequest", Assert.Single(stranger.Errors).Code);
    }
}
