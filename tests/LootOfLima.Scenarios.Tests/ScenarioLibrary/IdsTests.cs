using Sut = LootOfLima.Scenarios.ScenarioLibrary;

namespace LootOfLima.Scenarios.Tests.ScenarioLibrary;

public class IdsTests
{
    [Fact]
    public void Should_be_distinct_when_the_ids_are_listed()
    {
        // Arrange
        IReadOnlyList<string> ids = Sut.Ids;

        // Act
        int distinct = ids.Distinct().Count();

        // Assert
        Assert.Equal(ids.Count, distinct);
    }

    [Fact]
    public void Should_omit_b16s12_when_the_ids_are_listed()
    {
        // Arrange
        // Book 16 prints scenario 12's tokens as a duplicate of scenario 11's, which contradicts
        // its own question table. That is an error in the printed book, not in the importer.
        const string brokenInThePrintedBook = "b16s12";

        // Act
        IReadOnlyList<string> actual = Sut.Ids;

        // Assert
        Assert.DoesNotContain(brokenInThePrintedBook, actual);
    }
}
