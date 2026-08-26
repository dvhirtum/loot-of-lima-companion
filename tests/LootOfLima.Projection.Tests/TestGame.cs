using LootOfLima.Rules;
using Board = LootOfLima.Rules.BoardLayout;
using Location = LootOfLima.Rules.LocationId;
using Setup = LootOfLima.Rules.Deal;

namespace LootOfLima.Projection.Tests;

/// <summary>
/// Builds games at a given point in their life, so a test about what one viewer may see does not
/// spend twenty lines getting there.
/// </summary>
internal static class TestGame
{
    public const int DefaultSeed = 20260825;

    public static Board Layout => Board.Default;

    /// <summary>Seats, the first <paramref name="humans"/> of them human and the rest bots.</summary>
    public static IReadOnlyList<SeatSpec> Seats(int playerCount, int humans) =>
    [
        .. Enumerable.Range(0, playerCount).Select(index => index < humans
            ? new SeatSpec(SeatKind.Human, $"Player {index + 1}", $"player-{index + 1}")
            : new SeatSpec(SeatKind.Bot, $"Bot {index + 1}")),
    ];

    /// <summary>
    /// A deal. Ask for <see cref="DealSourceKind.Scenario"/> to get one that skips setup passing,
    /// the way the printed books do.
    /// </summary>
    public static Setup Deal(int playerCount, DealSourceKind kind = DealSourceKind.Generated)
    {
        Setup generated = Dealer.Create(playerCount, DefaultSeed);
        if (kind == DealSourceKind.Generated)
        {
            return generated;
        }

        // A real scenario deal carries the book's printed "fewest terrain" letters, which settle
        // the setup announcement for every seat. Stand in for them with the first truthful choice.
        IReadOnlyList<Terrain?> prescribed =
            [.. generated.Hands.Select(hand => (Terrain?)FewestTerrainRule.Minima(hand)[0])];

        return Setup.Create(
            generated.Hands,
            generated.PublicReveal,
            generated.Treasure,
            DealSource.Scenario("test-scenario"),
            prescribed);
    }

    public static GameState Lobby(int playerCount, int humans) =>
        Unwrap(GameEngine.CreateGame("game-1", "ROOM", Seats(playerCount, humans), DefaultSeed));

    /// <summary>Ready to play: dealt, tokens taken, and any passing done.</summary>
    public static GameState InProgress(
        int playerCount = 5,
        int humans = 3,
        DealSourceKind kind = DealSourceKind.Generated)
    {
        GameState state = Unwrap(GameEngine.Start(
            Lobby(playerCount, humans), Deal(playerCount, kind), new DeterministicShuffleSource(DefaultSeed)));

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = Unwrap(GameEngine.ConfirmTokensTaken(state, seat.Index));
        }

        if (state.Phase == GamePhase.Passing)
        {
            foreach (Seat seat in state.HumanSeats.ToList())
            {
                state = Unwrap(GameEngine.ConfirmPassDone(state, seat.Index));
            }
        }

        // Each human names the first terrain it truthfully holds fewest of; bots and prescribed
        // seats settle themselves.
        while (state.Phase == GamePhase.Announcing && state.ActiveSeat is { } seatIndex)
        {
            Terrain terrain = FewestTerrainRule.Minima(state.SeatAt(seatIndex).Hand)[0];
            state = Unwrap(GameEngine.AnnounceFewestTerrain(state, seatIndex, terrain));
        }

        return state;
    }

    /// <summary>Dealt and passed, waiting on the first seat to announce its fewest terrain.</summary>
    public static GameState Announcing(int playerCount = 5, int humans = 3)
    {
        GameState state = Unwrap(GameEngine.Start(
            Lobby(playerCount, humans), Deal(playerCount), new DeterministicShuffleSource(DefaultSeed)));

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = Unwrap(GameEngine.ConfirmTokensTaken(state, seat.Index));
        }

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = Unwrap(GameEngine.ConfirmPassDone(state, seat.Index));
        }

        return state;
    }

    /// <summary>A game somebody has already won, so the treasure is out in the open.</summary>
    public static GameState Finished(int playerCount = 5, int humans = 3)
    {
        GameState state = InProgress(playerCount, humans);
        return Unwrap(GameEngine.Accuse(state, 0, state.Deal!.Treasure[0], state.Deal.Treasure[1]));
    }

    /// <summary>A location that is definitely not part of the treasure, for deliberately wrong guesses.</summary>
    public static Location NotTreasure(GameState state, int skip = 0) =>
        Location.All.Where(location => !state.Deal!.Treasure.Contains(location)).Skip(skip).First();

    /// <summary>Asserts the command succeeded, and hands back the new state.</summary>
    public static GameState Unwrap(Result<GameState> result)
    {
        Assert.Null(result.Error);
        return result.Value;
    }
}
