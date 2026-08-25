namespace LootOfLima.Rules;

/// <summary>
/// Everything that has happened, in order.
/// </summary>
/// <remarks>
/// Every entry type lives in this file, and the projection switches over them exhaustively. Three
/// of them carry information that must never reach a third party intact, and the projection
/// redacts those by mapping them to a different type rather than by blanking fields, so a
/// non-participant receives a DTO that has no field for the secret in the first place.
/// </remarks>
/// <param name="Sequence">Position in the history, from zero.</param>
/// <param name="TurnNumber">The turn it happened on. Zero for anything before play starts.</param>
public abstract record HistoryEntry(int Sequence, int TurnNumber);

/// <summary>A question asked with the dice. The answer is truthful and public.</summary>
public sealed record PublicQuestionEntry(
    int Sequence,
    int TurnNumber,
    int AskerSeat,
    int TargetSeat,
    QueryRange Range,
    int Answer) : HistoryEntry(Sequence, TurnNumber);

/// <summary>
/// A question asked with the Pistol. PRIVATE: only the asker and the target may ever see the range
/// or the answer. Everyone else learns only that it happened, and between whom.
/// </summary>
public sealed record SecretQuestionEntry(
    int Sequence,
    int TurnNumber,
    int AskerSeat,
    int TargetSeat,
    QueryRange Range,
    int Answer) : HistoryEntry(Sequence, TurnNumber);

/// <summary>An ability token spent. Public: everyone can see the token leave the player board.</summary>
public sealed record AbilityUsedEntry(
    int Sequence,
    int TurnNumber,
    int SeatIndex,
    AbilityType Ability) : HistoryEntry(Sequence, TurnNumber);

/// <summary>
/// A setup token pass. PRIVATE: which token moved is known only to the giver and the receiver.
/// That a pass happened, and between whom, is plainly visible at the table and so stays public.
/// </summary>
public sealed record TokenPassEntry(
    int Sequence,
    int TurnNumber,
    PassRecord Pass) : HistoryEntry(Sequence, TurnNumber);

/// <summary>
/// An accusation. When it is wrong the named locations are PRIVATE to the accuser: the rulebook
/// says an incorrect accusation reveals nothing, and knowing which pair had been ruled out would be
/// a real deduction. A correct accusation names the treasure, so it is public.
/// </summary>
public sealed record AccusationEntry(
    int Sequence,
    int TurnNumber,
    int AccuserSeat,
    IReadOnlyList<LocationId> Named,
    bool Correct) : HistoryEntry(Sequence, TurnNumber);

/// <summary>The game is over. Public, including the winner if there is one.</summary>
public sealed record GameEndedEntry(
    int Sequence,
    int TurnNumber,
    int? WinnerSeat,
    GameEndReason Reason) : HistoryEntry(Sequence, TurnNumber);

/// <summary>
/// A setup announcement of the terrain a seat holds fewest of. Public: it is announced aloud.
/// </summary>
/// <remarks>
/// Carries only the terrain named, never how many terrains were eligible. The rulebook is explicit
/// that a player breaking a tie must not reveal that there was one.
/// </remarks>
public sealed record FewestTerrainAnnouncedEntry(
    int Sequence,
    int TurnNumber,
    int SeatIndex,
    Terrain Terrain) : HistoryEntry(Sequence, TurnNumber);
