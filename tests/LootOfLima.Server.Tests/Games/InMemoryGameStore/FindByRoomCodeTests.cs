using LootOfLima.Rules;
using Stored = LootOfLima.Server.Games.StoredGame;
using Sut = LootOfLima.Server.Games.InMemoryGameStore;

namespace LootOfLima.Server.Tests.Games.InMemoryGameStore;

public class FindByRoomCodeTests
{
    [Theory]
    [InlineData("ABCD")]
    [InlineData("abcd")]
    [InlineData("AbCd")]
    public async Task Should_find_the_game_when_the_code_is_typed_in_any_case(string typed)
    {
        // Arrange
        // The code is read off a screen and typed on a phone, so case is the player's business.
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby("game-1", "ABCD"), TestContext.Current.CancellationToken);

        // Act
        Stored? found = await store.FindByRoomCodeAsync(typed, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("game-1", found?.State.GameId);
    }

    [Fact]
    public async Task Should_find_nothing_when_no_game_holds_that_code()
    {
        // Arrange
        Sut store = new();
        await store.TryAddAsync(TestGames.Lobby("game-1", "ABCD"), TestContext.Current.CancellationToken);

        // Act
        Stored? found = await store.FindByRoomCodeAsync("WXYZ", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(found);
    }
}
