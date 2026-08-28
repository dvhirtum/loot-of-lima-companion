using LootOfLima.Rules;
using Stored = LootOfLima.Server.Games.StoredGame;
using Sut = LootOfLima.Server.Games.InMemoryGameStore;

namespace LootOfLima.Server.Tests.Games.InMemoryGameStore;

public class MutateTests
{
    [Fact]
    public async Task Should_store_the_new_state_when_the_command_succeeds()
    {
        // Arrange
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby(), TestContext.Current.CancellationToken);

        // Act
        Result<Stored>? outcome = await store.MutateAsync(
            "game-1",
            state => GameEngine.ClaimSeat(state, 1, "player-2", "Grace"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(outcome is { Error: null });
        GameState stored = (await store.FindAsync("game-1", TestContext.Current.CancellationToken))!.State;
        Assert.Equal("player-2", stored.SeatAt(1).PlayerId);
    }

    [Fact]
    public async Task Should_leave_the_game_untouched_when_the_command_is_rejected()
    {
        // Arrange
        // A rejected Result holds no value at all. Writing one back would either throw or, worse,
        // land an illegal move, so the store writes only on success.
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby(), TestContext.Current.CancellationToken);

        // Act
        Result<Stored>? outcome = await store.MutateAsync(
            "game-1",
            state => GameEngine.ClaimSeat(state, 0, "player-2", "Grace"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(GameErrorCode.SeatAlreadyClaimed, outcome?.Error?.Code);
        GameState stored = (await store.FindAsync("game-1", TestContext.Current.CancellationToken))!.State;
        Assert.Equal("player-1", stored.SeatAt(0).PlayerId);
    }

    [Fact]
    public async Task Should_report_nothing_when_there_is_no_such_game()
    {
        // Arrange
        // Deliberately not a GameError: the rules engine has no concept of a game it cannot find,
        // and inventing one would put a lookup failure into the rulebook.
        Sut store = new();

        // Act
        Result<Stored>? outcome = await store.MutateAsync(
            "game-missing", Result<GameState>.Success, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(outcome);
    }

    [Fact]
    public async Task Should_seat_exactly_one_of_them_when_several_claim_the_same_seat_at_once()
    {
        // Arrange
        // Two people tapping the same card is the ordinary shape of a lobby filling up: five cards,
        // and everybody looking at the same screen in the middle of the table. Without the gate all
        // four would read the same state, all four would build one with themselves in seat 1, and
        // the last write would decide it -- having told the other three they were in.
        //
        // The sibling test below claims four *different* seats, so it would pass on a store that
        // merged writes. This one cannot: the four commands contradict each other.
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby(), TestContext.Current.CancellationToken);

        // Act
        // Task.Run so the calls genuinely overlap, and the pause holds each read open long enough
        // that an unguarded store would let all four read the same original state.
        Result<Stored>?[] outcomes = await Task.WhenAll(Enumerable.Range(1, 4).Select(attempt => Task.Run(
            () => store.MutateAsync(
                "game-1",
                state =>
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(25));
                    return GameEngine.ClaimSeat(state, 1, $"player-{attempt + 1}", $"Player {attempt + 1}");
                },
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        // Assert
        // One winner, three told so, and the seat holding the winner rather than the last writer.
        Assert.Single(outcomes, outcome => outcome is { Error: null });
        Assert.Equal(3, outcomes.Count(outcome => outcome?.Error?.Code == GameErrorCode.SeatAlreadyClaimed));

        GameState stored = (await store.FindAsync("game-1", TestContext.Current.CancellationToken))!.State;
        Assert.Equal(2, stored.HumanSeats.Count());
    }

    [Fact]
    public async Task Should_keep_every_command_when_they_all_arrive_at_once()
    {
        // Arrange
        // The race this store exists to prevent: a game is a value, so four connections doing
        // read-modify-write would each derive a new state from the same old one and only the last
        // write would survive. Every seat claimed here must still be claimed at the end.
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby(), TestContext.Current.CancellationToken);

        // Act
        // Task.Run so the calls genuinely overlap — awaiting them in sequence would run each one
        // to completion first and race nothing. The pause inside the command holds the read open
        // long enough that an unguarded store would let all four read the same original state.
        await Task.WhenAll(Enumerable.Range(1, 4).Select(seat => Task.Run(
            () => store.MutateAsync(
                "game-1",
                state =>
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(25));
                    return GameEngine.ClaimSeat(state, seat, $"player-{seat + 1}", $"Player {seat + 1}");
                },
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        // Assert
        GameState stored = (await store.FindAsync("game-1", TestContext.Current.CancellationToken))!.State;
        Assert.Equal(5, stored.HumanSeats.Count());
    }
}
