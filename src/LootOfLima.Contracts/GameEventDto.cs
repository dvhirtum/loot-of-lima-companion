namespace LootOfLima.Contracts;

/// <summary>
/// A nudge that something happened, so a client can animate or chime.
/// </summary>
/// <remarks>
/// Deliberately thin. The authoritative state is always the <see cref="PlayerView"/> that arrives
/// alongside it, already projected for that viewer; an event carries no game detail of its own, so
/// it cannot become a second, unprojected channel for state to escape through.
/// </remarks>
/// <param name="Kind">
/// What happened: <c>SeatClaimed</c>, <c>SeatReleased</c>, <c>GameStarted</c>, <c>TurnAdvanced</c>,
/// <c>QuestionAsked</c>, <c>AbilitySpent</c>, <c>AccusationMade</c> or <c>GameEnded</c>.
/// </param>
/// <param name="SeatIndex">Whose action it was, when that is public anyway.</param>
public sealed record GameEventDto(string Kind, int? SeatIndex = null);
