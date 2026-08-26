namespace LootOfLima.Rules;

/// <summary>Why the game ended.</summary>
public enum GameEndReason
{
    /// <summary>Somebody named both treasure locations correctly.</summary>
    CorrectAccusation = 0,

    /// <summary>Everyone else accused wrongly, so the last player standing wins.</summary>
    LastPlayerStanding = 1,

    /// <summary>Every human seat has been eliminated. Nobody wins.</summary>
    AllPlayersEliminated = 2,
}
