// Every test folder namespace shadows the production type it is named after, so the types this
// file needs are aliased back in. An alias only wins the lookup if its own name is not also a test
// folder name, hence Setup, Deals, Play and friends.
using Board = LootOfLima.Rules.BoardLayout;
using Deals = LootOfLima.Rules.Dealer;
using FewestRule = LootOfLima.Rules.FewestTerrainRule;
using Location = LootOfLima.Rules.LocationId;
using Play = LootOfLima.Rules.GameEngine;
using RandomSource = LootOfLima.Rules.DeterministicShuffleSource;
using Setup = LootOfLima.Rules.Deal;
using StateResult = LootOfLima.Rules.Result<LootOfLima.Rules.GameState>;

namespace LootOfLima.Rules.Tests;

/// <summary>
/// Builds games at a given point in their life, so a test that is about one rule does not spend
/// twenty lines getting there.
/// </summary>
internal static class TestGame
{
    public const int DefaultSeed = 20260825;

    public static Board Layout => Board.Default;

    /// <summary>Seats, first <paramref name="humans"/> of them human and the rest bots.</summary>
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
    public static Setup Deal(
        int playerCount,
        DealSourceKind kind = DealSourceKind.Generated,
        int seed = DefaultSeed)
    {
        Setup generated = Deals.Create(playerCount, seed);
        if (kind == DealSourceKind.Generated)
        {
            return generated;
        }

        // A real scenario deal carries the book's printed "fewest terrain" letters, which settle
        // the setup announcement for every seat. Stand in for them with the first truthful choice.
        IReadOnlyList<Terrain?> prescribed =
            [.. generated.Hands.Select(hand => (Terrain?)FewestRule.Minima(hand)[0])];

        return Setup.Create(
            generated.Hands,
            generated.PublicReveal,
            generated.Treasure,
            DealSource.Scenario("test-scenario"),
            prescribed);
    }

    public static GameState Lobby(int playerCount, int humans = 1, int randomSeed = DefaultSeed) =>
        Unwrap(Play.CreateGame("game-1", "ROOM", Seats(playerCount, humans), randomSeed));

    /// <summary>Dealt, waiting for every human to say they picked their tokens up.</summary>
    /// <param name="seed">Seeds the deal and the passing shuffle.</param>
    /// <param name="randomSeed">
    /// Seeds the game itself, which is what breaks a bot's fewest-terrain tie. Defaults to
    /// <paramref name="seed"/>, so asking for a different game changes everything about it. Pass
    /// it on its own to hold the deal still and vary nothing but the tie-break.
    /// </param>
    public static GameState Dealing(
        int playerCount,
        int humans = 1,
        DealSourceKind kind = DealSourceKind.Generated,
        int seed = DefaultSeed,
        int? randomSeed = null) =>
        Unwrap(Play.Start(
            Lobby(playerCount, humans, randomSeed ?? seed),
            Deal(playerCount, kind, seed),
            Shuffle(seed)));

    /// <summary>Ready to play: dealt, tokens taken, and any passing done. Seeded as Dealing is.</summary>
    public static GameState InProgress(
        int playerCount,
        int humans = 1,
        DealSourceKind kind = DealSourceKind.Generated,
        int seed = DefaultSeed,
        int? randomSeed = null)
    {
        GameState state = Dealing(playerCount, humans, kind, seed, randomSeed);

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = Unwrap(Play.ConfirmTokensTaken(state, seat.Index));
        }

        if (state.Phase == GamePhase.Passing)
        {
            foreach (Seat seat in state.HumanSeats.ToList())
            {
                state = Unwrap(Play.ConfirmPassDone(state, seat.Index));
            }
        }

        return AnnounceAll(state);
    }

    /// <summary>
    /// Walks the setup announcement to its end, each human naming the first terrain it truthfully
    /// holds fewest of. Bots and prescribed seats settle themselves.
    /// </summary>
    public static GameState AnnounceAll(GameState state)
    {
        while (state.Phase == GamePhase.Announcing && state.ActiveSeat is { } seatIndex)
        {
            Terrain terrain = FewestRule.Minima(state.SeatAt(seatIndex).Hand)[0];
            state = Unwrap(Play.AnnounceFewestTerrain(state, seatIndex, terrain));
        }

        return state;
    }

    public static IShuffleSource Shuffle(int seed = DefaultSeed) => new RandomSource(seed);

    /// <summary>A location that is definitely not part of the treasure, for deliberately wrong guesses.</summary>
    public static Location NotTreasure(GameState state, int skip = 0) =>
        Location.All.Where(location => !state.Deal!.Treasure.Contains(location)).Skip(skip).First();

    /// <summary>Asserts the command succeeded, and hands back the new state.</summary>
    public static GameState Unwrap(StateResult result)
    {
        Assert.Null(result.Error);
        return result.Value;
    }

    /// <summary>Asserts the command was rejected, and hands back why.</summary>
    public static GameErrorCode Rejection(StateResult result)
    {
        Assert.NotNull(result.Error);
        return result.Error.Code;
    }
}
