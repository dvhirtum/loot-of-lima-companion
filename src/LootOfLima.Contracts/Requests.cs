namespace LootOfLima.Contracts;

/// <summary>
/// What a client asks for. Every value here is a plain string or an int for the same reason
/// <see cref="PlayerView"/> names locations as strings: this assembly deliberately cannot reference
/// the rules engine, so <c>Direction</c>, <c>Terrain</c> and <c>LocationId</c> are unreachable from
/// any hub signature. The server parses them and rejects what it cannot read.
/// </summary>
/// <remarks>
/// A request never carries a seat index saying who is asking. Identity comes from the caller's
/// token, never from the request body — a client that could name its own seat could name somebody
/// else's.
/// </remarks>
/// <param name="Start">A compass direction: N, NE, E, SE, S, SW, W or NW.</param>
/// <param name="End">Where the arc stops, exclusive. Equal to <paramref name="Start"/> sweeps all eight.</param>
/// <param name="Terrain">Beach, Forest or Mountain, or null for an unrestricted question.</param>
public sealed record AskQuestionRequest(int TargetSeat, string Start, string End, string? Terrain = null);

/// <summary>
/// A Pistol question. Shaped exactly like <see cref="AskQuestionRequest"/>, terrain included.
/// </summary>
/// <remarks>
/// The Pistol frees the asker from the dice, not from the shapes a question may take, so a secret
/// question may name a terrain like any other. It carries no seat index for the same reason no
/// request does.
/// </remarks>
public sealed record AskSecretQuestionRequest(int TargetSeat, string Start, string End, string? Terrain = null);

/// <summary>An accusation naming the two locations the accuser believes are the treasure.</summary>
public sealed record AccuseRequest(string First, string Second);

/// <summary>Starting a game: which deal to play.</summary>
/// <param name="ScenarioId">
/// An official scenario such as <c>b1s1</c>, or null to have the app generate a deal. Official
/// scenarios are five-player only.
/// </param>
public sealed record StartGameRequest(string? ScenarioId = null);
