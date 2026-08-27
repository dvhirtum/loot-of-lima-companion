namespace LootOfLima.Rules;

/// <summary>Why the game ended.</summary>
public enum GameEndReason
{
    /// <summary>Somebody named both treasure locations correctly.</summary>
    CorrectAccusation = 0,

    /// <summary>Everyone else accused wrongly, so the last player standing wins.</summary>
    LastPlayerStanding = 1,

    /// <summary>
    /// Every human seat has been eliminated. Nobody wins. No accusation produces this any more —
    /// a solo game does not eliminate, and a larger one ends at the last player standing — so it
    /// survives only as the fall-through of the turn loop, which has to return something.
    /// </summary>
    AllPlayersEliminated = 2,

    /// <summary>
    /// The only player at the table asked to be shown the treasure. Reachable solely from a solo
    /// game, and solely after a wrong accusation: nothing else ends a game nobody can be eliminated
    /// out of.
    /// </summary>
    Conceded = 3,
}
