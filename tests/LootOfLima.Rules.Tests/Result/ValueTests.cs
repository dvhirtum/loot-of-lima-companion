using Sut = LootOfLima.Rules.Result<string>;

namespace LootOfLima.Rules.Tests.Result;

public class ValueTests
{
    [Fact]
    public void Should_hand_back_the_value_when_the_command_succeeded()
    {
        // Arrange
        Sut result = Sut.Success("done");

        // Act
        string value = result.Value;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("done", value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Should_throw_when_the_command_failed()
    {
        // Arrange
        Sut result = Sut.Failure(new GameError(GameErrorCode.NotYourTurn, "not now"));

        // Act
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => result.Value);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("NotYourTurn", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_not_claim_success_when_the_result_is_the_struct_default()
    {
        // Arrange
        // No constructor runs for a struct's default, so it has neither a value nor an error.
        // Reporting that as success would hand the caller a null.
        Sut result = default;

        // Act
        bool success = result.IsSuccess;

        // Assert
        Assert.False(success);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Should_throw_when_a_success_is_built_from_nothing()
    {
        // Arrange
        // Act
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Sut.Success(null!));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Should_convert_an_error_into_a_failure_when_a_guard_returns_one()
    {
        // Arrange
        // The implicit conversion is what lets a guard return its error without naming the result
        // type again, which is how every command in GameEngine is written.
        GameError error = new(GameErrorCode.WrongPhase, "too early");

        // Act
        Sut result = error;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
    }
}
