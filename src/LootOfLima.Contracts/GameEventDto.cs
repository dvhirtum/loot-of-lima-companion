namespace LootOfLima.Contracts;

/// <summary>
/// A nudge that something happened, so a client can animate or chime.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately thin. The authoritative state is always the <see cref="PlayerView"/> that arrives
/// alongside it, already projected for that viewer; an event carries no game detail of its own, so
/// it cannot become a second, unprojected channel for state to escape through.
/// </para>
/// <para>
/// The kinds are the constants below and nothing else. They were once a list in this comment and a
/// separate set of literals in the hub, which drifted in both directions at once: two kinds were
/// documented and never sent, and one was sent and never documented. Naming them here and using
/// them there leaves one list, which cannot disagree with itself.
/// </para>
/// <para>
/// There is deliberately no kind for a turn passing or a game ending. Both are plain to read from
/// the view arriving alongside — the active seat moved, the phase is <c>Finished</c> — and an event
/// that only restates the view is a second thing to keep true.
/// </para>
/// </remarks>
/// <param name="Kind">One of the constants on this type.</param>
/// <param name="SeatIndex">Whose action it was, when that is public anyway.</param>
public sealed record GameEventDto(string Kind, int? SeatIndex = null)
{
    public const string SeatClaimed = nameof(SeatClaimed);

    public const string SeatReleased = nameof(SeatReleased);

    public const string GameStarted = nameof(GameStarted);

    public const string FewestTerrainAnnounced = nameof(FewestTerrainAnnounced);

    public const string QuestionAsked = nameof(QuestionAsked);

    public const string AbilitySpent = nameof(AbilitySpent);

    public const string AccusationMade = nameof(AccusationMade);

    /// <summary>Every kind this server sends, for a client that wants to check it knows them all.</summary>
    public static readonly IReadOnlyList<string> AllKinds =
    [
        SeatClaimed,
        SeatReleased,
        GameStarted,
        FewestTerrainAnnounced,
        QuestionAsked,
        AbilitySpent,
        AccusationMade,
    ];
}
