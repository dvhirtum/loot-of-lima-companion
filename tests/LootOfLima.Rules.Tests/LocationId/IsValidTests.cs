using Sut = LootOfLima.Rules.LocationId;

namespace LootOfLima.Rules.Tests.LocationId;

public class IsValidTests
{
    [Fact]
    public void Should_be_false_when_the_location_is_the_struct_default()
    {
        // Arrange
        Sut location = default;

        // Act
        bool actual = location.IsValid;

        // Assert
        Assert.False(actual);
    }

    [Fact]
    public void Should_be_false_when_a_failed_TryParse_leaves_its_out_parameter_behind()
    {
        // Arrange
        // (TryParse assigns default before reporting failure, so a caller ignoring the bool gets it)

        // Act
        bool parsed = Sut.TryParse("nonsense", out Sut location);

        // Assert
        Assert.False(parsed);
        Assert.False(location.IsValid);
    }

    [Fact]
    public void Should_be_true_when_the_location_is_one_of_the_24()
    {
        // Arrange
        // (nothing to set up - LocationId.All is the full board)

        // Act
        bool actual = Sut.All.All(location => location.IsValid);

        // Assert
        Assert.True(actual);
    }
}
