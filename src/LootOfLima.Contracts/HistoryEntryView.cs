using System.Text.Json.Serialization;

namespace LootOfLima.Contracts;

/// <summary>
/// One thing that happened, as one viewer is allowed to see it.
/// </summary>
/// <remarks>
/// <para>
/// Redaction here is by <em>type</em>, never by blanking a field. A player who was not party to a
/// secret question receives a <see cref="SecretQuestionOccurredView"/>, which has no property for
/// the range or the answer, so there is nothing for a nulled field, a forgotten branch or a
/// serializer setting to leak. The same pattern covers token passes and failed accusations.
/// </para>
/// <para>
/// The discriminator is written as a <c>type</c> property so the wire format stays legible and a
/// crude substring assertion over a raw frame remains a meaningful test.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PublicQuestionView), "publicQuestion")]
[JsonDerivedType(typeof(SecretQuestionView), "secretQuestion")]
[JsonDerivedType(typeof(SecretQuestionOccurredView), "secretQuestionOccurred")]
[JsonDerivedType(typeof(AbilityUsedView), "abilityUsed")]
[JsonDerivedType(typeof(TokenPassView), "tokenPass")]
[JsonDerivedType(typeof(TokenPassOccurredView), "tokenPassOccurred")]
[JsonDerivedType(typeof(AccusationView), "accusation")]
[JsonDerivedType(typeof(AccusationOccurredView), "accusationOccurred")]
[JsonDerivedType(typeof(GameEndedView), "gameEnded")]
public abstract record HistoryEntryView(int Sequence, int TurnNumber);

/// <summary>A dice question and its answer. Everybody sees these in full.</summary>
/// <param name="Terrain">Beach, Forest or Mountain, or null when the question was unrestricted.</param>
public sealed record PublicQuestionView(
    int Sequence,
    int TurnNumber,
    int AskerSeat,
    int TargetSeat,
    string Start,
    string End,
    string? Terrain,
    int Answer) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>A Pistol question, as its asker and its target see it.</summary>
public sealed record SecretQuestionView(
    int Sequence,
    int TurnNumber,
    int AskerSeat,
    int TargetSeat,
    string Start,
    string End,
    int Answer) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>
/// The same Pistol question as everybody else sees it: that it happened, and between whom. There
/// is deliberately no property here that could carry the range or the answer.
/// </summary>
public sealed record SecretQuestionOccurredView(
    int Sequence,
    int TurnNumber,
    int AskerSeat,
    int TargetSeat) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>An ability token spent. Public, because the token visibly leaves the player board.</summary>
public sealed record AbilityUsedView(
    int Sequence,
    int TurnNumber,
    int SeatIndex,
    string Ability) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>A setup token pass, as its giver and its receiver see it.</summary>
public sealed record TokenPassView(
    int Sequence,
    int TurnNumber,
    int GiverSeat,
    int ReceiverSeat,
    string Location,
    string Direction) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>
/// The same pass as everybody else sees it. Which token moved is private; that it moved, and
/// between whom, is plainly visible across the table.
/// </summary>
public sealed record TokenPassOccurredView(
    int Sequence,
    int TurnNumber,
    int GiverSeat,
    int ReceiverSeat,
    string Direction) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>An accusation with the pair it named: a correct one for everybody, a wrong one only
/// for the accuser.</summary>
public sealed record AccusationView(
    int Sequence,
    int TurnNumber,
    int AccuserSeat,
    IReadOnlyList<string> Named,
    bool Correct) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>
/// A wrong accusation as everybody else sees it. The rulebook says an incorrect accusation reveals
/// nothing, and a pair ruled out is a real deduction, so there is no property for the pair.
/// </summary>
public sealed record AccusationOccurredView(
    int Sequence,
    int TurnNumber,
    int AccuserSeat) : HistoryEntryView(Sequence, TurnNumber);

/// <summary>The game ended. Public.</summary>
public sealed record GameEndedView(
    int Sequence,
    int TurnNumber,
    int? WinnerSeat,
    string Reason) : HistoryEntryView(Sequence, TurnNumber);
