namespace LootOfLima.Rules;

/// <summary>Why a command was rejected. Stable enough for a client to switch on.</summary>
public enum GameErrorCode
{
    UnsupportedPlayerCount,
    SeatOutOfRange,
    WrongPhase,
    SeatAlreadyClaimed,
    PlayerAlreadySeated,
    SeatNotClaimed,
    NoHumanSeats,
    DealDoesNotFitGame,
    NotYourTurn,
    SeatIsEliminated,
    SeatIsNotHuman,
    CannotTargetYourself,
    AbilityAlreadySpent,
    PistolNotSpentThisTurn,
    TerrainFilterNotAllowed,
    NotAFewestTerrain,
    InvalidAccusation,
    AlreadyConfirmed,
}

/// <summary>A rejected command. Commands never throw for rule violations; they return one of these.</summary>
public sealed record GameError(GameErrorCode Code, string Message)
{
    public override string ToString() => $"{Code}: {Message}";
}
