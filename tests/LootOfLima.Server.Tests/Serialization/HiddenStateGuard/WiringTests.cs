using System.Text.Json;
using LootOfLima.Contracts;
using LootOfLima.Rules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJson = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace LootOfLima.Server.Tests.Serialization.HiddenStateGuard;

/// <summary>
/// That the guard is actually in front of every serializer, and not merely written.
/// </summary>
/// <remarks>
/// <para>
/// The guard's own tests ask it directly, which proves it refuses what it should and stands aside
/// for the rest. They cannot prove it is ever consulted. These reach into the built application
/// and ask the very options the framework will serialize through, which is the only way to catch
/// the failure the guard is most likely to suffer: still there, still correct, and wired to
/// nothing.
/// </para>
/// <para>
/// Both exits are checked because the server has two. Nothing on the REST side goes near hidden
/// state today, and the intention is that nothing ever will — which is exactly why the check
/// belongs here rather than in whoever eventually writes the endpoint that would have leaked.
/// </para>
/// </remarks>
public class WiringTests
{
    [Theory]
    [InlineData(typeof(GameState))]
    [InlineData(typeof(Deal))]
    [InlineData(typeof(Seat))]
    [InlineData(typeof(SecretQuestionEntry))]
    [InlineData(typeof(TokenPassEntry))]
    [InlineData(typeof(AccusationEntry))]
    public async Task Should_refuse_hidden_state_when_the_rest_serializer_is_asked(Type forbidden)
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();

        // Act
        JsonSerializerOptions json = RestOptions(app);

        // Assert
        Assert.Throws<InvalidOperationException>(
            () => json.TypeInfoResolver!.GetTypeInfo(forbidden, json));
    }

    [Theory]
    [InlineData(typeof(GameState))]
    [InlineData(typeof(Deal))]
    [InlineData(typeof(Seat))]
    [InlineData(typeof(SecretQuestionEntry))]
    [InlineData(typeof(TokenPassEntry))]
    [InlineData(typeof(AccusationEntry))]
    public async Task Should_refuse_hidden_state_when_the_hub_serializer_is_asked(Type forbidden)
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();

        // Act
        JsonSerializerOptions json = HubOptions(app);

        // Assert
        Assert.Throws<InvalidOperationException>(
            () => json.TypeInfoResolver!.GetTypeInfo(forbidden, json));
    }

    [Fact]
    public async Task Should_still_resolve_the_view_when_the_hub_serializer_is_asked()
    {
        // Arrange
        // The guard answers null for everything it allows, so a chain holding only the guard would
        // resolve nothing at all — including the arguments arriving from clients. That mistake once
        // took the whole hub down with "Error binding arguments", and only an integration test
        // found it, so it is worth asserting rather than assuming.
        await using WebApplicationFactory<Program> app = new();

        // Act
        JsonSerializerOptions json = HubOptions(app);

        // Assert
        Assert.NotNull(json.TypeInfoResolver!.GetTypeInfo(typeof(PlayerView), json));
        Assert.NotNull(json.TypeInfoResolver.GetTypeInfo(typeof(AskQuestionRequest), json));
    }

    [Fact]
    public async Task Should_still_resolve_the_lobby_when_the_rest_serializer_is_asked()
    {
        // Arrange
        await using WebApplicationFactory<Program> app = new();

        // Act
        JsonSerializerOptions json = RestOptions(app);

        // Assert
        Assert.NotNull(json.TypeInfoResolver!.GetTypeInfo(typeof(LobbySummary), json));
        Assert.NotNull(json.TypeInfoResolver.GetTypeInfo(typeof(CreateGameResponse), json));
    }

    /// <summary>
    /// The options minimal API responses go through.
    /// </summary>
    /// <remarks>
    /// A client is created first because the factory builds its host lazily, and asking an unbuilt
    /// host for its options would configure one on the spot and answer about that instead.
    /// </remarks>
    private static JsonSerializerOptions RestOptions(WebApplicationFactory<Program> app)
    {
        using HttpClient started = app.CreateClient();
        return app.Services.GetRequiredService<IOptions<HttpJson>>().Value.SerializerOptions;
    }

    /// <summary>The options the hub's JSON protocol sends and receives through.</summary>
    private static JsonSerializerOptions HubOptions(WebApplicationFactory<Program> app)
    {
        using HttpClient started = app.CreateClient();
        return app.Services
            .GetRequiredService<IOptions<JsonHubProtocolOptions>>()
            .Value.PayloadSerializerOptions;
    }
}
