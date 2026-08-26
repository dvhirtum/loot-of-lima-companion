using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LootOfLima.Contracts;
using LootOfLima.Rules;
using Sut = LootOfLima.Server.Serialization.HiddenStateGuard;

namespace LootOfLima.Server.Tests.Serialization.HiddenStateGuard;

public class GetTypeInfoTests
{
    [Theory]
    [InlineData(typeof(GameState))]
    [InlineData(typeof(Deal))]
    [InlineData(typeof(Seat))]
    [InlineData(typeof(SecretQuestionEntry))]
    public void Should_refuse_the_type_when_it_carries_hidden_state(Type forbidden)
    {
        // Arrange
        // The type system already makes these unreachable from a hub signature. This is the case no
        // compiler check can see: secret state arriving at the serializer as an object.
        Sut guard = new();

        // Act
        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
            () => guard.GetTypeInfo(forbidden, JsonSerializerOptions.Default));

        // Assert
        Assert.Contains(forbidden.Name, thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(PlayerView))]
    [InlineData(typeof(SeatView))]
    [InlineData(typeof(ErrorDto))]
    [InlineData(typeof(string))]
    public void Should_stand_aside_when_the_type_is_one_a_client_may_have(Type allowed)
    {
        // Arrange
        // Null means "not mine", handing the type to the rest of the resolver chain. A guard that
        // answered here would quietly become the only resolver in the application.
        Sut guard = new();

        // Act
        JsonTypeInfo? resolved = guard.GetTypeInfo(allowed, JsonSerializerOptions.Default);

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public void Should_refuse_a_collection_of_secrets_when_it_is_asked_for_the_element_type()
    {
        // Arrange
        // Serializing a list asks for the element's contract, so barring the element bars the list.
        Sut guard = new();

        // Act
        // Assert
        Assert.Throws<InvalidOperationException>(
            () => guard.GetTypeInfo(typeof(Seat), JsonSerializerOptions.Default));
    }
}
