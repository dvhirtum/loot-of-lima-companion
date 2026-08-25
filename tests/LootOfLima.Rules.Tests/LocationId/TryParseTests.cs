namespace LootOfLima.Rules.Tests;

public class TryParseTests
{
    [Theory]
    [InlineData("")]
    [InlineData("B")]
    [InlineData("B0")]
    [InlineData("B9")]
    [InlineData("X1")]
    [InlineData("B12")]
    public void Should_return_false_when_the_text_is_not_a_location(string text)
    {
        // Arrange
        // (the malformed input is supplied by the theory data)

        // Act
        bool actual = LocationId.TryParse(text, out _);

        // Assert
        Assert.False(actual);
    }
}
