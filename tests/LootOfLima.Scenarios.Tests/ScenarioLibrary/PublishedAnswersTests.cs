using Sut = LootOfLima.Scenarios.ScenarioLibrary;

namespace LootOfLima.Scenarios.Tests.ScenarioLibrary;

public class PublishedAnswersTests
{
    [Theory]
    [MemberData(nameof(ScenarioIds.All), MemberType = typeof(ScenarioIds))]
    public void Should_return_a_full_answer_block_when_any_scenario_is_asked_for(string id)
    {
        // Arrange
        int expected = Sut.AnswersPerScenario;

        // Act
        int actual = Sut.PublishedAnswers(id).Length;

        // Assert
        Assert.Equal(expected, actual);
    }
}
