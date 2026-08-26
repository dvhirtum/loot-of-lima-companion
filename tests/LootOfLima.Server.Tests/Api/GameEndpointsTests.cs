using System.Net;
using System.Net.Http.Json;
using LootOfLima.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LootOfLima.Server.Tests.Api;

/// <summary>
/// The endpoints a client reaches before it has a connection. Grouped by endpoint rather than split
/// one class per method, because they are one small surface and each test is a request and a check.
/// </summary>
public class GameEndpointsTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Should_open_a_table_when_a_game_is_created(int playerCount)
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/games", new CreateGameRequest(playerCount), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        CreateGameResponse created = (await response.Content.ReadFromJsonAsync<CreateGameResponse>(
            TestContext.Current.CancellationToken))!;

        Assert.Equal(4, created.RoomCode.Length);
        Assert.EndsWith($"/join/{created.RoomCode}", created.JoinUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1000000000)]
    [InlineData(2)]
    [InlineData(6)]
    public async Task Should_refuse_the_table_when_the_game_is_for_the_wrong_number_of_players(int playerCount)
    {
        // Arrange
        // The engine decides this, not the endpoint: the box says three to five.
        // A count the engine would refuse must not be turned into a seat list first: a negative one
        // throws before the check is reached, and a billion allocates for a long time before it.
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/games", new CreateGameRequest(playerCount), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_give_a_different_code_to_every_table_when_several_are_opened()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();

        // Act
        List<string> codes = [];
        for (int table = 0; table < 20; table++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/games", new CreateGameRequest(5), TestContext.Current.CancellationToken);

            codes.Add((await response.Content.ReadFromJsonAsync<CreateGameResponse>(
                TestContext.Current.CancellationToken))!.RoomCode);
        }

        // Assert
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Should_show_the_seats_and_no_secrets_when_the_lobby_is_read()
    {
        // Arrange
        // Reachable with no credential at all, so it carries what is written on the outside of the
        // box and nothing else. There is deliberately no field here for a hand or a deal.
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();
        CreateGameResponse game = await OpenTableAsync(client);

        // Act
        LobbySummary? lobby = await client.GetFromJsonAsync<LobbySummary>(
            $"/api/games/{game.RoomCode}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Lobby", lobby!.Phase);
        Assert.Equal(5, lobby.PlayerCount);
        Assert.All(lobby.Seats, seat => Assert.False(seat.Taken));
    }

    [Fact]
    public async Task Should_find_the_lobby_when_the_code_is_typed_in_lower_case()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();
        CreateGameResponse game = await OpenTableAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/games/{game.RoomCode.ToLowerInvariant()}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_answer_not_found_when_no_table_holds_the_code()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync(
            "/api/games/ZZZZ", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Should_hand_out_an_identity_when_a_player_joins()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();
        CreateGameResponse game = await OpenTableAsync(client);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/games/{game.RoomCode}/join",
            new JoinRequest("Ada"),
            TestContext.Current.CancellationToken);

        // Assert
        JoinResponse joined = (await response.Content.ReadFromJsonAsync<JoinResponse>(
            TestContext.Current.CancellationToken))!;

        Assert.NotEmpty(joined.PlayerToken);
        Assert.NotEqual(joined.PlayerToken, joined.PlayerId);
    }

    [Fact]
    public async Task Should_claim_no_seat_when_a_player_joins()
    {
        // Arrange
        // Joining is arriving, not sitting down. Which seat is the player's own choice, and it is
        // made over the hub where the rest of the table watches the lobby fill up.
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();
        CreateGameResponse game = await OpenTableAsync(client);

        // Act
        await client.PostAsJsonAsync(
            $"/api/games/{game.RoomCode}/join",
            new JoinRequest("Ada"),
            TestContext.Current.CancellationToken);

        // Assert
        LobbySummary? lobby = await client.GetFromJsonAsync<LobbySummary>(
            $"/api/games/{game.RoomCode}", TestContext.Current.CancellationToken);

        Assert.All(lobby!.Seats, seat => Assert.False(seat.Taken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_refuse_the_join_when_the_player_gives_no_name(string displayName)
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();
        CreateGameResponse game = await OpenTableAsync(client);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/games/{game.RoomCode}/join",
            new JoinRequest(displayName),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_draw_a_png_of_the_join_url_when_the_qr_code_is_fetched()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();
        CreateGameResponse game = await OpenTableAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"/api/games/{game.RoomCode}/qr.png", TestContext.Current.CancellationToken);

        // Assert
        byte[] png = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);

        // The eight-byte PNG signature, so this is a real image rather than an error page.
        Assert.Equal<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
    }

    [Fact]
    public async Task Should_list_every_decoded_scenario_when_the_scenarios_are_fetched()
    {
        // Arrange
        // 239 of the 240 printed scenarios. b16s12 is absent because the book contradicts itself.
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();

        // Act
        IReadOnlyList<ScenarioSummary>? scenarios = await client.GetFromJsonAsync<IReadOnlyList<ScenarioSummary>>(
            "/api/scenarios", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(239, scenarios!.Count);
        Assert.All(scenarios, scenario => Assert.Equal(5, scenario.PlayerCount));
        Assert.DoesNotContain(scenarios, scenario => scenario.Id == "b16s12");
    }

    [Fact]
    public async Task Should_serve_the_island_when_the_board_is_fetched()
    {
        // Arrange
        // Served rather than drawn twice: the geometry was recovered by fitting it against the
        // published books, and a second copy in the client would be a second chance to get it wrong.
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();

        // Act
        BoardLayoutDto? board = await client.GetFromJsonAsync<BoardLayoutDto>(
            "/api/board", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(8, board!.Sectors.Count);
        Assert.All(board.Sectors, sector => Assert.Equal(3, sector.Locations.Count));
        Assert.Equal(24, board.Sectors.SelectMany(sector => sector.Locations).Distinct().Count());
        Assert.True(board.Arc is { StartInclusive: true, EndExclusive: true, StartEqualsEndMeansFullCircle: true });
    }

    [Fact]
    public async Task Should_put_sector_k_in_the_kth_sector_when_the_board_is_fetched()
    {
        // Arrange
        // Sector k holds Bk, Fk and Mk, N through NW. This is the fact the whole oracle rests on,
        // so it is worth asserting on the wire as well as in the engine.
        await using WebApplicationFactory<Program> app = new();
        HttpClient client = app.CreateClient();

        // Act
        BoardLayoutDto? board = await client.GetFromJsonAsync<BoardLayoutDto>(
            "/api/board", TestContext.Current.CancellationToken);

        // Assert
        string[] compass = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        for (int sector = 0; sector < compass.Length; sector++)
        {
            BoardSector found = board!.Sectors[sector];
            Assert.Equal(compass[sector], found.Direction);
            Assert.Equal([$"B{sector + 1}", $"F{sector + 1}", $"M{sector + 1}"], found.Locations.Order());
        }
    }

    private static async Task<CreateGameResponse> OpenTableAsync(HttpClient client, int playerCount = 5)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/games", new CreateGameRequest(playerCount), TestContext.Current.CancellationToken);

        return (await response.Content.ReadFromJsonAsync<CreateGameResponse>(
            TestContext.Current.CancellationToken))!;
    }
}
