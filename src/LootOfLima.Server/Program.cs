using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using LootOfLima.Server.Api;
using LootOfLima.Server.Games;
using LootOfLima.Server.Hubs;
using LootOfLima.Server.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGameStore, InMemoryGameStore>();
builder.Services.AddSingleton<IPlayerTokenStore, InMemoryPlayerTokenStore>();
builder.Services.AddSingleton<IConnectionRegistry, InMemoryConnectionRegistry>();
builder.Services.AddSingleton<GameBroadcaster>();

// Both ways out of this process, because the guard's whole claim is that nothing gets contract
// metadata without passing it first, and that was only true of the hub. Nothing on the REST side
// goes near hidden state today -- the lobby summary is what is written on the outside of the box --
// but a belt that covers one of two exits is not a belt.
builder.Services
    .AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment())
    .AddJsonProtocol(options => GuardHiddenState(options.PayloadSerializerOptions));

builder.Services.ConfigureHttpJsonOptions(options => GuardHiddenState(options.SerializerOptions));

WebApplication app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
app.MapGameEndpoints();
app.MapHub<GameHub>("/hub/game");

await app.RunAsync();

/// <summary>
/// Puts <see cref="HiddenStateGuard"/> at the front of a serializer's resolver chain.
/// </summary>
/// <remarks>
/// The guard goes first so nothing reaches the serializer without passing it, and whatever was
/// already configured stays behind it to do the actual work. It cannot simply replace the chain:
/// a resolver that answers null for everything it allows resolves nothing at all, so on its own it
/// would leave the application unable to read even the arguments arriving from clients.
/// </remarks>
static void GuardHiddenState(JsonSerializerOptions options) =>
    options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        new HiddenStateGuard(),
        options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());

/// <summary>
/// Named so the integration tests can bootstrap the real application through
/// <c>WebApplicationFactory</c> rather than stand up a second, divergent composition root.
/// </summary>
public partial class Program;
