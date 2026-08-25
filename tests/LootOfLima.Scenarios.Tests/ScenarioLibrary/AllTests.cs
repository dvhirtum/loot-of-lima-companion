using Sut = LootOfLima.Scenarios.ScenarioLibrary;

namespace LootOfLima.Scenarios.Tests.ScenarioLibrary;

public class AllTests
{
    [Fact]
    public void Should_hold_239_scenarios_when_the_library_is_loaded()
    {
        // Arrange
        // 240 are printed across the 20 books; one is dropped, see ScenarioLibrary.Ids tests.
        const int printedMinusTheBrokenOne = 239;

        // Act
        int actual = Sut.All.Count;

        // Assert
        Assert.Equal(printedMinusTheBrokenOne, actual);
    }

    [Fact]
    public void Should_span_all_twenty_books_when_the_library_is_loaded()
    {
        // Arrange
        int[] expected = [.. Enumerable.Range(1, 20)];

        // Act
        int[] actual = Sut.All.Select(s => s.Book).Distinct().Order().ToArray();

        // Assert
        Assert.Equal(expected, actual);
    }
}
