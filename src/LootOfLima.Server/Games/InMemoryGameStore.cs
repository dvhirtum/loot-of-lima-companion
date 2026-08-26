using System.Collections.Concurrent;
using LootOfLima.Rules;

namespace LootOfLima.Server.Games;

/// <summary>
/// Games live in this process and die with it, which is the whole of M3's persistence story.
/// </summary>
/// <remarks>
/// A game sits on the table for an evening, and a lost server means a re-deal rather than a lost
/// heirloom. The interface is async and the seam is real, so M6 can put SQLite behind it without
/// anything above noticing.
/// </remarks>
internal sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<string, Entry> _byGameId = new(StringComparer.Ordinal);

    /// <summary>Room codes are typed by people, so they match without regard to case.</summary>
    private readonly ConcurrentDictionary<string, string> _gameIdByRoomCode =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> TryAddAsync(GameState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        // The room code goes in first: it is the one thing that has to be unique across live games,
        // and claiming it before the game exists means two simultaneous creations cannot both win.
        if (!_gameIdByRoomCode.TryAdd(state.RoomCode, state.GameId))
        {
            return Task.FromResult(false);
        }

        if (!_byGameId.TryAdd(state.GameId, new Entry(state)))
        {
            _gameIdByRoomCode.TryRemove(new KeyValuePair<string, string>(state.RoomCode, state.GameId));
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<StoredGame?> FindAsync(string gameId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byGameId.TryGetValue(gameId, out Entry? entry) ? entry.Game : null);

    public Task<StoredGame?> FindByRoomCodeAsync(string roomCode, CancellationToken cancellationToken = default) =>
        _gameIdByRoomCode.TryGetValue(roomCode, out string? gameId)
            ? FindAsync(gameId, cancellationToken)
            : Task.FromResult<StoredGame?>(null);

    public async Task<Result<StoredGame>?> MutateAsync(
        string gameId,
        Func<GameState, Result<GameState>> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_byGameId.TryGetValue(gameId, out Entry? entry))
        {
            return null;
        }

        await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Result<GameState> result = command(entry.Game.State);
            if (result.Error is { } error)
            {
                return Result<StoredGame>.Failure(error);
            }

            // Stored and numbered in one step, still holding the gate. The caller is handed the
            // version its own command produced rather than whatever the store is at by the time it
            // asks, which would already be a later command's answer.
            entry.Game = new StoredGame(result.Value, entry.Game.Version + 1);
            return Result<StoredGame>.Success(entry.Game);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    /// <summary>
    /// One game and the gate that serialises commands against it. The gate is per game rather than
    /// global so a busy table cannot slow down the one next to it.
    /// </summary>
    private sealed class Entry(GameState state)
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>Only ever written while <see cref="Gate"/> is held.</summary>
        public StoredGame Game { get; set; } = new(state, Version: 0);
    }
}
