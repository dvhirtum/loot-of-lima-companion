using Sut = LootOfLima.Rules.DeterministicShuffleSource;

namespace LootOfLima.Rules.Tests.DeterministicShuffleSource;

public class NextTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(24)]
    [InlineData(1000)]
    public void Should_stay_inside_the_bound_when_many_values_are_drawn(int bound)
    {
        // Arrange
        Sut source = new(TestGame.DefaultSeed);

        // Act
        List<int> values = [.. Enumerable.Range(0, 10_000).Select(_ => source.Next(bound))];

        // Assert
        Assert.All(values, value => Assert.InRange(value, 0, bound - 1));
    }

    [Fact]
    public void Should_produce_the_same_sequence_when_the_seed_is_repeated()
    {
        // Arrange
        // A game stores only its seed, so this is what makes a deal reproducible at all.
        Sut first = new(7);
        Sut second = new(7);

        // Act
        List<int> a = [.. Enumerable.Range(0, 200).Select(_ => first.Next(24))];
        List<int> b = [.. Enumerable.Range(0, 200).Select(_ => second.Next(24))];

        // Assert
        Assert.Equal(a, b);
    }

    [Fact]
    public void Should_produce_a_different_sequence_when_the_seed_differs()
    {
        // Arrange
        Sut first = new(7);
        Sut second = new(8);

        // Act
        List<int> a = [.. Enumerable.Range(0, 200).Select(_ => first.Next(24))];
        List<int> b = [.. Enumerable.Range(0, 200).Select(_ => second.Next(24))];

        // Assert
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Should_reach_every_value_when_the_bound_is_the_size_of_the_island()
    {
        // Arrange
        Sut source = new(TestGame.DefaultSeed);

        // Act
        HashSet<int> seen = [.. Enumerable.Range(0, 5_000).Select(_ => source.Next(24))];

        // Assert
        Assert.Equal(24, seen.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_throw_when_the_bound_is_not_positive(int bound)
    {
        // Arrange
        Sut source = new(TestGame.DefaultSeed);

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => source.Next(bound));

        // Assert
        Assert.Equal("exclusiveUpperBound", exception.ParamName);
    }
}
