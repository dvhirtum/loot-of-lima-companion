namespace LootOfLima.Scenarios.Tests;

public class PublishedAnswersTests
{
    [Theory]
    [MemberData(nameof(ScenarioIds.All), MemberType = typeof(ScenarioIds))]
    public void Should_return_a_full_answer_block_when_any_scenario_is_asked_for(string id)
    {
        // Arrange
        int expected = ScenarioLibrary.AnswersPerScenario;

        // Act
        int actual = ScenarioLibrary.PublishedAnswers(id).Length;

        // Assert
        Assert.Equal(expected, actual);
    }
}
