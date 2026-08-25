namespace LootOfLima.Rules;

/// <summary>How a seat is to be filled when the game is created.</summary>
public sealed record SeatSpec(SeatKind Kind, string DisplayName, string? PlayerId = null);

/// <summary>
/// One player's place at the table. Immutable: every command returns a new seat rather than
/// mutating one, which is what makes the whole engine safe to reason about.
/// </summary>
/// <param name="Hand">Empty until the deal, then kept sorted by location index.</param>
/// <param name="AbilitiesRemaining">Starts as all three; each is spendable once per game.</param>
/// <param name="Eliminated">Set by a wrong accusation. An eliminated seat still answers questions.</param>
/// <param name="TokensConfirmed">The player has picked up the physical tokens the app dealt them.</param>
/// <param name="PassConfirmed">The player has handed over the physical tokens the app told them to pass.</param>
/// <param name="PistolArmedTurn">
/// The turn on which this seat spent its Pistol. A secret question is legal only while this equals
/// the current turn number, so the ability unlocks exactly one question on exactly one turn.
/// </param>
public sealed record Seat(
    int Index,
    SeatKind Kind,
    string DisplayName,
    string? PlayerId,
    IReadOnlyList<LocationId> Hand,
    IReadOnlyList<AbilityType> AbilitiesRemaining,
    bool Eliminated,
    bool TokensConfirmed,
    bool PassConfirmed,
    int? PistolArmedTurn)
{
    public bool IsHuman => Kind == SeatKind.Human;

    public bool HasAbility(AbilityType ability) => AbilitiesRemaining.Contains(ability);

    /// <summary>An unstarted seat: no hand, all three abilities, nothing confirmed.</summary>
    public static Seat Create(int index, SeatSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return new Seat(
            Index: index,
            Kind: spec.Kind,
            DisplayName: spec.DisplayName,
            PlayerId: spec.PlayerId,
            Hand: [],
            AbilitiesRemaining: [.. Abilities.All],
            Eliminated: false,
            TokensConfirmed: false,
            PassConfirmed: false,
            PistolArmedTurn: null);
    }

    public override string ToString() => $"seat {Index} ({DisplayName}, {Kind})";
}
