using LootOfLima.Rules;
using Stored = LootOfLima.Server.Games.StoredGame;
using Sut = LootOfLima.Server.Games.InMemoryGameStore;

namespace LootOfLima.Server.Tests.Games.InMemoryGameStore;

public class TryAddTests
{
    [Fact]
    public async Task Should_file_the_game_when_its_room_code_is_free()
    {
        // Arrange
        Sut store = new();
        GameState game = TestGames.Lobby();

        // Act
        bool added = await store.TryAddAsync(game, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(added);
        Stored? found = await store.FindAsync(game.GameId, TestContext.Current.CancellationToken);
        Assert.Equal(game.GameId, found?.State.GameId);
    }

    [Fact]
    public async Task Should_refuse_the_game_when_the_room_code_is_already_live()
    {
        // Arrange
        // Two tables holding the same code would send a joining phone to whichever the lookup
        // happened to find, so the second one is refused and the caller draws another code.
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby("game-1", "ABCD"), TestContext.Current.CancellationToken);

        // Act
        bool added = await store.TryAddAsync(
            TestGames.Lobby("game-2", "abcd"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(added);
        Assert.Null(await store.FindAsync("game-2", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_keep_the_first_game_reachable_when_a_second_is_refused()
    {
        // Arrange
        // A refused add must not take the winner's room code down with it on the way out.
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby("game-1", "ABCD"), TestContext.Current.CancellationToken);

        // Act
        await store.TryAddAsync(TestGames.Lobby("game-2", "ABCD"), TestContext.Current.CancellationToken);

        // Assert
        Stored? found = await store.FindByRoomCodeAsync("ABCD", TestContext.Current.CancellationToken);
        Assert.Equal("game-1", found?.State.GameId);
    }
}
