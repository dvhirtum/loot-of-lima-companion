using Sut = LootOfLima.Rules.Passing;

namespace LootOfLima.Rules.Tests.Passing;

public class IsRequiredTests
{
    [Fact]
    public void Should_not_pass_when_there_are_three_players()
    {
        // Arrange
        // Act
        bool required = Sut.IsRequired(3, DealSourceKind.Generated);

        // Assert
        Assert.False(required);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_pass_when_the_table_is_bigger_and_the_deal_is_generated(int playerCount)
    {
        // Arrange
        // Act
        bool required = Sut.IsRequired(playerCount, DealSourceKind.Generated);

        // Assert
        Assert.True(required);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Should_not_pass_when_the_deal_comes_from_a_printed_scenario(int playerCount)
    {
        // Arrange
        // The books replace passing with the "fewest terrain" hint, and their published answers
        // describe the hands exactly as dealt. Passing would contradict the book.

        // Act
        bool required = Sut.IsRequired(playerCount, DealSourceKind.Scenario);

        // Assert
        Assert.False(required);
    }
}
