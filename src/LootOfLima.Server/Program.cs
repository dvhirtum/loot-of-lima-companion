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

builder.Services
    .AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment())
    .AddJsonProtocol(options =>
        // The guard first, so nothing reaches the serializer without passing it, and the default
        // resolver behind it to do the actual work. Inserting the guard into the chain on its own
        // would leave it the only resolver, and a resolver that answers null for everything it
        // allows resolves nothing at all — including the arguments arriving from clients.
        options.PayloadSerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
            new HiddenStateGuard(),
            new DefaultJsonTypeInfoResolver()));

WebApplication app = builder.Build();

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
app.MapGameEndpoints();
app.MapHub<GameHub>("/hub/game");

await app.RunAsync();

/// <summary>
/// Named so the integration tests can bootstrap the real application through
/// <c>WebApplicationFactory</c> rather than stand up a second, divergent composition root.
/// </summary>
public partial class Program;
