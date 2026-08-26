namespace LootOfLima.Rules;

/// <summary>The game's lifecycle.</summary>
/// <remarks>
/// Seat claiming happens throughout <see cref="Lobby"/> rather than in a phase of its own: there is
/// no event that would move a game from "created" to "claiming", so a separate phase would be a
/// state nothing can ever be observed in.
/// </remarks>
public enum GamePhase
{
    /// <summary>Created; seats can be claimed and released.</summary>
    Lobby = 0,

    /// <summary>Dealt; every human must confirm they picked up their physical tokens.</summary>
    Dealing = 1,

    /// <summary>Tokens are being passed. Skipped entirely when passing does not apply.</summary>
    Passing = 2,

    /// <summary>
    /// In turn order, each player announces the terrain they hold fewest of. Applies at every table
    /// size, and comes after passing because passing changes what a player holds fewest of.
    /// </summary>
    Announcing = 3,

    /// <summary>Questions, abilities and accusations.</summary>
    InProgress = 4,

    /// <summary>Over. This is the only phase in which the treasure is projected to anyone.</summary>
    Finished = 5,
}
