using LootOfLima.Rules;

namespace LootOfLima.Server.Games;

/// <summary>
/// A game as the store holds it, and the revision it is at.
/// </summary>
/// <param name="Version">
/// Counts the commands that have been applied to this game, and is assigned under the very lock
/// that applied one, so no two states of a game ever carry the same number and the order of the
/// numbers is the order things actually happened in.
/// </param>
/// <remarks>
/// The version exists because commands are ordered but their broadcasts are not. Each command
/// commits under the store's gate and then sends its frames afterwards, over separate connections,
/// as separate awaits — and nothing makes those arrive in the order they were started. Two players
/// acting at the same moment can therefore leave a client holding the earlier of the two states.
/// Stamping the frame does not stop the overtaking; it makes it visible, which is enough, because
/// a viewer that has already seen a higher version can simply drop what arrives late.
/// </remarks>
internal sealed record StoredGame(GameState State, long Version);
