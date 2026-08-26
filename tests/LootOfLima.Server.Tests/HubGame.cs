using LootOfLima.Contracts;
using LootOfLima.Rules;

namespace LootOfLima.Server.Tests;

/// <summary>
/// Drives a game to a given point through the hub, exactly as a client would.
/// </summary>
/// <remarks>
/// <para>
/// Where the game has got to is read from the server rather than from a client's last frame.
/// Everybody watching is sent a frame whenever anybody acts, so a client's view is only as current
/// as its own delivery — fine to assert on once things have settled, but no way to decide what to
/// do next. Asking the server is the test scaffolding being honest about being scaffolding.
/// </para>
/// <para>
/// What a player <em>decides</em> still comes only from what that player can see. The setup
/// announcement is worked out from the announcer's own <see cref="SeatView.KnownHand"/>: if a real
/// client could not compute it, neither may this.
/// </para>
/// </remarks>
internal static class HubGame
{
    /// <summary>
    /// Seats <paramref name="humans"/> players, deals, and walks setup until play begins.
    /// </summary>
    /// <returns>The seated clients, in seat order.</returns>
    public static async Task<IReadOnlyList<TestClient>> PlayToInProgressAsync(
        HubHarness harness,
        int humans = 2,
        string? scenarioId = null)
    {
        List<TestClient> players = [];
        for (int seat = 0; seat < humans; seat++)
        {
            TestClient client = await harness.JoinAsync(harness.IssueToken($"Player {seat + 1}"));
            await harness.ActAsync(client, "ClaimSeat", seat);
            players.Add(client);
        }

        await harness.ActAsync(players[0], "StartGame", new StartGameRequest(scenarioId));

        foreach (TestClient player in players)
        {
            await harness.ActAsync(player, "ConfirmTokensTaken");
        }

        if ((await harness.CurrentAsync()).Phase == GamePhase.Passing)
        {
            foreach (TestClient player in players)
            {
                await harness.ActAsync(player, "ConfirmPassDone");
            }
        }

        await AnnounceAllAsync(harness, players);

        Assert.All(players, player => Assert.Empty(player.Errors));
        return players;
    }

    /// <summary>
    /// Walks the setup announcement, each seated player naming a terrain it truthfully holds fewest
    /// of. Bots and prescribed seats settle themselves, so this only ever drives human seats.
    /// </summary>
    private static async Task AnnounceAllAsync(HubHarness harness, IReadOnlyList<TestClient> players)
    {
        GameState state = await harness.CurrentAsync();
        while (state is { Phase: GamePhase.Announcing, ActiveSeat: { } active })
        {
            TestClient announcing = players.FirstOrDefault(player => player.View.ViewerSeat == active)
                ?? throw new InvalidOperationException($"Seat {active} must announce but is nobody's.");

            await harness.ActAsync(announcing, "AnnounceFewestTerrain", FewestTerrainOf(announcing.View));
            state = await harness.CurrentAsync();
        }
    }

    /// <summary>
    /// The terrain this viewer holds fewest of, counted from the hand the view gives them.
    /// </summary>
    /// <remarks>
    /// Locations are named <c>B1</c>, <c>F3</c>, <c>M8</c>, so the terrain is the first character —
    /// which is all a real client has to work with either. On a tie this takes the first, and that
    /// is a legal choice: the rule is that the announcement must be truthful, not that it must be
    /// the only truthful one.
    /// </remarks>
    private static string FewestTerrainOf(PlayerView view)
    {
        SeatView own = view.Seats[view.ViewerSeat!.Value];

        (char Code, string Name)[] terrains = [('B', "Beach"), ('F', "Forest"), ('M', "Mountain")];

        return terrains
            .OrderBy(terrain => own.KnownHand.Count(location => location[0] == terrain.Code))
            .First()
            .Name;
    }
}
