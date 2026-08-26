using LootOfLima.Server.Games;
using Sut = LootOfLima.Server.Games.InMemoryPlayerTokenStore;

namespace LootOfLima.Server.Tests.Games.InMemoryPlayerTokenStore;

public class IssueTests
{
    [Fact]
    public void Should_mint_a_distinct_token_and_player_id_for_every_join()
    {
        // Arrange
        // The token is a bearer credential: holding it is holding the seat and the hand that comes
        // with it. Two players sharing either value would be the same person to this server.
        Sut store = new();
        List<string> tokens = [];
        List<string> playerIds = [];

        // Act
        for (int join = 0; join < 200; join++)
        {
            PlayerIdentity identity = store.Issue("game-1", $"Player {join}", out string token);
            tokens.Add(token);
            playerIds.Add(identity.PlayerId);
        }

        // Assert
        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(playerIds.Count, playerIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Should_keep_the_token_out_of_the_player_id_when_one_is_issued()
    {
        // Arrange
        // The player id is written onto a seat and travels in views; the token never leaves the
        // owner. Deriving one from the other would put the credential on the wire.
        Sut store = new();

        // Act
        PlayerIdentity identity = store.Issue("game-1", "Ada", out string token);

        // Assert
        Assert.DoesNotContain(token, identity.PlayerId, StringComparison.Ordinal);
        Assert.DoesNotContain(identity.PlayerId, token, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_throw_when_the_display_name_is_not_a_name(string displayName)
    {
        // Arrange
        Sut store = new();

        // Act
        // Assert
        Assert.Throws<ArgumentException>(() => store.Issue("game-1", displayName, out _));
    }
}
