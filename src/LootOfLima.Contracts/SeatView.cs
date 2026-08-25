namespace LootOfLima.Contracts;

/// <summary>
/// One seat as one viewer sees it.
/// </summary>
/// <param name="Kind">Human or Bot.</param>
/// <param name="HandSize">How many locations this seat holds. Public: hand sizes never change.</param>
/// <param name="KnownHand">
/// The locations the viewer is entitled to know are in this hand. That is the whole hand for the
/// viewer, and for any other seat only what setup passing told them.
/// </param>
/// <param name="HandIsComplete">
/// True when <see cref="KnownHand"/> is the entire hand, which is only ever the case for the
/// viewer themselves. A client must not infer anything from a short list without this flag.
/// </param>
/// <param name="AbilitiesRemaining">Unspent ability tokens. Public: they sit face up on the board.</param>
public sealed record SeatView(
    int Index,
    string Kind,
    string DisplayName,
    bool IsViewer,
    bool IsActive,
    bool Eliminated,
    int HandSize,
    IReadOnlyList<string> KnownHand,
    bool HandIsComplete,
    IReadOnlyList<string> AbilitiesRemaining,
    bool TokensConfirmed,
    bool PassConfirmed);
