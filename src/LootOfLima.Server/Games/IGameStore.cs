using LootOfLima.Rules;

namespace LootOfLima.Server.Games;

/// <summary>
/// Where live games are kept.
/// </summary>
/// <remarks>
/// Commands go through <see cref="MutateAsync"/> rather than a read-then-write pair, because a
/// game is a value: two connections applying commands to the same state would each derive a new
/// state from the same old one, and the second write would silently discard the first. The store
/// owns the read-modify-write so that race cannot be expressed. M6 may put EF Core behind this
/// interface, which is why it is async with nothing in memory demanding it.
/// </remarks>
internal interface IGameStore
{
    /// <summary>Files a newly created game. False if its room code is already live.</summary>
    Task<bool> TryAddAsync(GameState state, CancellationToken cancellationToken = default);

    Task<GameState?> FindAsync(string gameId, CancellationToken cancellationToken = default);

    Task<GameState?> FindByRoomCodeAsync(string roomCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies one engine command to a game, atomically. The new state is stored only when the
    /// command succeeds, so a rejected command cannot leave a half-applied game behind.
    /// </summary>
    /// <returns>
    /// What the command returned, or null when there is no such game. A missing game is not a
    /// <see cref="GameError"/>: the rules engine has no concept of a game it cannot find, and
    /// inventing a rule error for a lookup failure would put a server concern into the rulebook.
    /// </returns>
    Task<Result<GameState>?> MutateAsync(
        string gameId,
        Func<GameState, Result<GameState>> command,
        CancellationToken cancellationToken = default);
}
