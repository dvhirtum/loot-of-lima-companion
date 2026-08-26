using LootOfLima.Server.Games;
using Sut = LootOfLima.Server.Games.InMemoryPlayerTokenStore;

namespace LootOfLima.Server.Tests.Games.InMemoryPlayerTokenStore;

public class ResolveTests
{
    [Fact]
    public void Should_give_back_the_identity_when_the_token_was_issued()
    {
        // Arrange
        Sut store = new();
        PlayerIdentity issued = store.Issue("game-1", "Ada", out string token);

        // Act
        PlayerIdentity? resolved = store.Resolve(token);

        // Assert
        Assert.Equal(issued, resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void Should_give_back_nothing_when_the_token_was_never_issued(string? token)
    {
        // Arrange
        // An unknown token is a stranger, not an error: the caller turns this into a rejection.
        Sut store = new();
        store.Issue("game-1", "Ada", out _);

        // Act
        // Assert
        Assert.Null(store.Resolve(token));
    }
}
