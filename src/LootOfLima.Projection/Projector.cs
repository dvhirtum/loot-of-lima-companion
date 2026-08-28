using LootOfLima.Contracts;
using LootOfLima.Rules;

namespace LootOfLima.Projection;

/// <summary>
/// The hidden-information choke point: the single pure function that turns the secret
/// <see cref="GameState"/> into what one viewer is allowed to see.
/// </summary>
/// <remarks>
/// Nothing else may convert game state into a client-facing shape. Everything the server sends is
/// the output of this function, sent per connection, so a leak has exactly one place to hide.
/// </remarks>
public static class Projector
{
    /// <summary>
    /// Builds one viewer's view.
    /// </summary>
    /// <param name="viewerSeat">
    /// The seat to build the view for, or null for a spectator, who is entitled to strictly less
    /// than any player.
    /// </param>
    public static PlayerView Project(GameState state, int? viewerSeat)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (viewerSeat is { } seat && !state.IsSeatIndex(seat))
        {
            throw new ArgumentOutOfRangeException(nameof(viewerSeat), viewerSeat, "No such seat in this game.");
        }

        bool finished = state.Phase == GamePhase.Finished;

        return new PlayerView(
            GameId: state.GameId,
            RoomCode: state.RoomCode,
            Phase: state.Phase.ToString(),
            PlayerCount: state.PlayerCount,
            ViewerSeat: viewerSeat,
            ActiveSeat: state.ActiveSeat,
            TurnNumber: state.TurnNumber,
            Seats: [.. state.Seats.Select(s => ProjectSeat(state, s, viewerSeat))],
            PublicReveal: Names(state.Deal?.PublicReveal ?? []),

            // The treasure exists in the state from the moment of the deal. This ternary is the
            // only thing standing between it and the wire, which is why it lives here alone.
            Treasure: finished ? Names(state.Deal!.Treasure) : null,
            History: [.. state.History.Select(entry => ProjectEntry(entry, viewerSeat))],
            WinnerSeat: state.WinnerSeat,
            EndReason: state.EndReason?.ToString());
    }

    private static SeatView ProjectSeat(GameState state, Seat seat, int? viewerSeat)
    {
        bool isViewer = viewerSeat == seat.Index;

        // Own hand in full; any other hand only through what passing legitimately taught the
        // viewer. Never the hand itself, and never a count that could be differenced against one.
        IReadOnlyList<LocationId> known = isViewer ? seat.Hand : KnownFromPassing(state, seat.Index, viewerSeat);

        return new SeatView(
            Index: seat.Index,
            Kind: seat.Kind.ToString(),
            DisplayName: seat.DisplayName,
            IsViewer: isViewer,
            IsActive: state.ActiveSeat == seat.Index,
            Eliminated: seat.Eliminated,
            HandSize: seat.Hand.Count,
            KnownHand: Names(known),
            HandIsComplete: isViewer,
            AbilitiesRemaining: [.. seat.AbilitiesRemaining.Select(ability => ability.ToString())],

            // Truthfully, for every seat rather than only the viewer. It is derivable from the public
            // ability history either way, and a bool that read false for an armed seat would be a lie
            // rather than a redaction -- this app redacts by sending a different type, never by
            // blanking a field.
            PistolArmed: seat.PistolArmedTurn == state.TurnNumber,
            FewestTerrain: seat.FewestTerrain?.ToString(),
            TokensConfirmed: seat.TokensConfirmed,
            PassConfirmed: seat.PassConfirmed);
    }

    /// <summary>
    /// What the viewer knows about somebody else's hand: the tokens the viewer handed to that seat
    /// at setup.
    /// </summary>
    /// <remarks>
    /// Tokens the viewer <em>received</em> are deliberately absent. They say what the giver used to
    /// hold, not what they hold now, and they are already in the viewer's own hand.
    /// </remarks>
    private static IReadOnlyList<LocationId> KnownFromPassing(GameState state, int seatIndex, int? viewerSeat) =>
        viewerSeat is not { } viewer
            ? []
            : [.. state.PassRecords
                .Where(pass => pass.GiverSeat == viewer && pass.ReceiverSeat == seatIndex)
                .Select(pass => pass.Location)
                .Order()];

    private static HistoryEntryView ProjectEntry(HistoryEntry entry, int? viewerSeat) => entry switch
    {
        PublicQuestionEntry question => new PublicQuestionView(
            question.Sequence,
            question.TurnNumber,
            question.AskerSeat,
            question.TargetSeat,
            question.Range.Start.ToString(),
            question.Range.End.ToString(),
            question.Range.TerrainFilter?.ToString(),
            question.Answer),

        SecretQuestionEntry secret when viewerSeat == secret.AskerSeat || viewerSeat == secret.TargetSeat =>
            new SecretQuestionView(
                secret.Sequence,
                secret.TurnNumber,
                secret.AskerSeat,
                secret.TargetSeat,
                secret.Range.Start.ToString(),
                secret.Range.End.ToString(),
                secret.Range.TerrainFilter?.ToString(),
                secret.Answer),

        SecretQuestionEntry secret => new SecretQuestionOccurredView(
            secret.Sequence, secret.TurnNumber, secret.AskerSeat, secret.TargetSeat),

        AbilityUsedEntry ability => new AbilityUsedView(
            ability.Sequence, ability.TurnNumber, ability.SeatIndex, ability.Ability.ToString()),

        TokenPassEntry pass when viewerSeat == pass.Pass.GiverSeat || viewerSeat == pass.Pass.ReceiverSeat =>
            new TokenPassView(
                pass.Sequence,
                pass.TurnNumber,
                pass.Pass.GiverSeat,
                pass.Pass.ReceiverSeat,
                pass.Pass.Location.ToString(),
                pass.Pass.Direction.ToString()),

        TokenPassEntry pass => new TokenPassOccurredView(
            pass.Sequence,
            pass.TurnNumber,
            pass.Pass.GiverSeat,
            pass.Pass.ReceiverSeat,
            pass.Pass.Direction.ToString()),

        // A correct accusation names the treasure and ends the game, so it is public. A wrong one
        // reveals nothing, and a pair ruled out would be a real deduction.
        AccusationEntry accusation when accusation.Correct || viewerSeat == accusation.AccuserSeat =>
            new AccusationView(
                accusation.Sequence,
                accusation.TurnNumber,
                accusation.AccuserSeat,
                Names(accusation.Named),
                accusation.Correct),

        AccusationEntry accusation => new AccusationOccurredView(
            accusation.Sequence, accusation.TurnNumber, accusation.AccuserSeat),

        // Public in full: it is announced aloud. Note that the entry carries only the terrain
        // named, so there is nothing here that could betray whether the choice broke a tie.
        FewestTerrainAnnouncedEntry announced => new FewestTerrainAnnouncedView(
            announced.Sequence, announced.TurnNumber, announced.SeatIndex, announced.Terrain.ToString()),

        GameEndedEntry ended => new GameEndedView(
            ended.Sequence, ended.TurnNumber, ended.WinnerSeat, ended.Reason.ToString()),

        _ => throw new ArgumentOutOfRangeException(
            nameof(entry), entry, "This history entry has no projection, so it would leak by default."),
    };

    private static IReadOnlyList<string> Names(IEnumerable<LocationId> locations) =>
        [.. locations.Select(location => location.ToString())];
}
