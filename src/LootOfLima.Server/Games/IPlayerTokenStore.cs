namespace LootOfLima.Server.Games;

/// <summary>Who a connection is, independent of which seat they are sitting in.</summary>
/// <param name="GameId">The game the token was issued for. A token is worthless in any other.</param>
/// <param name="PlayerId">
/// The identity <see cref="LootOfLima.Rules.GameEngine.ClaimSeat"/> records on a seat. Deliberately
/// not a seat index: a player can release one seat and claim another, and the token must survive it.
/// </param>
internal sealed record PlayerIdentity(string GameId, string PlayerId, string DisplayName);

/// <summary>
/// Issues and resolves the opaque token a player keeps in local storage.
/// </summary>
/// <remarks>
/// The token is the only thing that says who a connection belongs to. Nothing is ever keyed on a
/// seat index the client sent: a client may ask to sit in seat 3, but who is asking is settled here
/// and never by the request.
/// </remarks>
internal interface IPlayerTokenStore
{
    /// <summary>Mints a token for a player joining a game, and the player id it stands for.</summary>
    PlayerIdentity Issue(string gameId, string displayName, out string token);

    /// <summary>Resolves a token, or null if it was never issued.</summary>
    PlayerIdentity? Resolve(string? token);
}
