using LootOfLima.Contracts;
using LootOfLima.Projection;
using LootOfLima.Rules;
using LootOfLima.Scenarios;

namespace LootOfLima.Harness;

/// <summary>Scripted games, played through the engine and printed as each seat sees them.</summary>
internal static class Replay
{
    private static readonly BoardLayout Layout = BoardLayout.Default;

    /// <summary>
    /// One of the printed books: five seats, one human, four bots, and no token passing — exactly
    /// the puzzle the solo books pose.
    /// </summary>
    public static void Scenario(Scenarios.Scenario scenario, int seed)
    {
        Console.WriteLine($"=== {scenario} ({scenario.Id}) ===");
        Console.WriteLine();

        GameState state = Unwrap(GameEngine.CreateGame(
            "demo-scenario",
            "LIMA",
            [
                new SeatSpec(SeatKind.Human, "You", "player-1"),
                .. Enumerable.Range(2, 4).Select(seat => new SeatSpec(SeatKind.Bot, $"Bot {seat}")),
            ],
            seed));

        state = Unwrap(GameEngine.Start(state, scenario.ToDeal(), new DeterministicShuffleSource(seed)));
        state = Unwrap(GameEngine.ConfirmTokensTaken(state, 0));

        PrintDeal(state);
        PrintAnnouncements(state, "the book prints these, so nobody is asked to choose");

        // Four ordinary questions, then a Pistol, then the winning accusation.
        state = Ask(state, 0, 1, new QueryRange(Direction.N, Direction.E));
        state = Ask(state, 0, 2, new QueryRange(Direction.N, Direction.SW, Terrain.Mountain));
        state = Ask(state, 0, 3, new QueryRange(Direction.S, Direction.NW, Terrain.Beach));
        state = Ask(state, 0, 4, new QueryRange(Direction.E, Direction.W));

        state = Unwrap(GameEngine.SpendAbility(state, 0, AbilityType.Pistol));
        Console.WriteLine("  turn 5  seat 0 spends its Pistol");
        state = AskSecretly(state, 0, 1, new QueryRange(Direction.SE, Direction.NE));

        IReadOnlyList<LocationId> treasure = state.Deal!.Treasure;
        Console.WriteLine($"  turn {state.TurnNumber}  seat 0 accuses {treasure[0]} and {treasure[1]}");
        state = Unwrap(GameEngine.Accuse(state, 0, treasure[0], treasure[1]));

        PrintOutcome(state);
        PrintViews(state);
    }

    /// <summary>
    /// A generated deal with two humans, so setup passing and a wrong accusation both get an
    /// airing. At four players each seat passes one token straight across the table.
    /// </summary>
    public static void Generated(int playerCount, int humans, int seed)
    {
        Console.WriteLine($"=== Generated {playerCount}-player game (seed {seed}) ===");
        Console.WriteLine();

        GameState state = Unwrap(GameEngine.CreateGame(
            "demo-generated",
            "LIMA",
            [
                .. Enumerable.Range(0, playerCount).Select(seat => seat < humans
                    ? new SeatSpec(SeatKind.Human, $"Player {seat + 1}", $"player-{seat + 1}")
                    : new SeatSpec(SeatKind.Bot, $"Bot {seat + 1}")),
            ],
            seed));

        state = Unwrap(GameEngine.Start(state, Dealer.Create(playerCount, seed), new DeterministicShuffleSource(seed)));

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = Unwrap(GameEngine.ConfirmTokensTaken(state, seat.Index));
        }

        PrintDeal(state);
        PrintPasses(state);

        foreach (Seat seat in state.HumanSeats.ToList())
        {
            state = Unwrap(GameEngine.ConfirmPassDone(state, seat.Index));
        }

        // Nothing is printed here for the humans to read: each announces for itself, and where two
        // terrains tie the choice is genuinely theirs. This script always takes the first.
        while (state.Phase == GamePhase.Announcing && state.ActiveSeat is { } announcer)
        {
            IReadOnlyList<Terrain> minima = FewestTerrainRule.Minima(state.SeatAt(announcer).Hand);
            state = Unwrap(GameEngine.AnnounceFewestTerrain(state, announcer, minima[0]));
        }

        PrintAnnouncements(state, "players chose their own; bots broke ties from the seed");

        state = Ask(state, 0, 2, new QueryRange(Direction.N, Direction.S));
        state = Ask(state, 1, 3, new QueryRange(Direction.SW, Direction.NE, Terrain.Forest));

        // Seat 0 guesses wrong, is eliminated, and seat 1 wins by being the last one standing.
        LocationId wrong = LocationId.All.First(location => !state.Deal!.Treasure.Contains(location));
        LocationId alsoWrong = LocationId.All.Last(location => !state.Deal!.Treasure.Contains(location));
        Console.WriteLine($"  turn {state.TurnNumber}  seat 0 accuses {wrong} and {alsoWrong}");
        state = Unwrap(GameEngine.Accuse(state, 0, wrong, alsoWrong));

        PrintOutcome(state);
        PrintViews(state);
    }

    private static GameState Ask(GameState state, int asker, int target, QueryRange range)
    {
        int turn = state.TurnNumber;
        GameState next = Unwrap(GameEngine.AskQuestion(state, asker, target, range, Layout));
        PublicQuestionEntry entry = next.History.OfType<PublicQuestionEntry>().Last();
        Console.WriteLine($"  turn {turn}  seat {asker} asks seat {target} {range} -> {entry.Answer}");
        return next;
    }

    private static GameState AskSecretly(GameState state, int asker, int target, QueryRange range)
    {
        int turn = state.TurnNumber;
        GameState next = Unwrap(GameEngine.AskSecretQuestion(state, asker, target, range, Layout));
        SecretQuestionEntry entry = next.History.OfType<SecretQuestionEntry>().Last();
        Console.WriteLine(
            $"  turn {turn}  seat {asker} asks seat {target} {range} SECRETLY -> {entry.Answer} "
            + "(only these two ever see this line)");
        return next;
    }

    private static void PrintDeal(GameState state)
    {
        Console.WriteLine($"  {state.PlayerCount} seats, {state.HumanSeats.Count()} human, {state.Deal!.Source}");
        foreach (Seat seat in state.Seats)
        {
            Console.WriteLine($"    seat {seat.Index}  {seat.DisplayName,-10} {string.Join(" ", seat.Hand)}");
        }

        Console.WriteLine($"    face up   {string.Join(" ", state.Deal.PublicReveal)}");
        Console.WriteLine();
    }

    private static void PrintPasses(GameState state)
    {
        if (!state.PassRecords.Any())
        {
            return;
        }

        Console.WriteLine("  setup passing");
        foreach (PassRecord pass in state.PassRecords)
        {
            Console.WriteLine($"    {pass}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// The setup announcements. A tie is marked only here, from the hands the harness can see —
    /// no player and no projection is ever told that a choice broke one.
    /// </summary>
    private static void PrintAnnouncements(GameState state, string note)
    {
        Console.WriteLine($"  fewest terrain announced ({note})");
        foreach (Seat seat in state.Seats)
        {
            int eligible = FewestTerrainRule.Minima(seat.Hand).Count;
            string tie = eligible > 1 ? $"   <- chosen from a {eligible}-way tie, which nobody else is told" : string.Empty;
            Console.WriteLine($"    seat {seat.Index}  {seat.FewestTerrain}{tie}");
        }

        Console.WriteLine();
    }

    private static void PrintOutcome(GameState state)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"  {state.Phase}: {state.EndReason}"
            + (state.WinnerSeat is { } winner ? $", seat {winner} wins" : ", nobody wins"));
        Console.WriteLine($"  the treasure was {string.Join(" and ", state.Deal!.Treasure)}");
        Console.WriteLine();
    }

    /// <summary>
    /// The point of the whole exercise: the same state, projected once per seat. Compare the
    /// secret-question and pass lines across seats to see the redaction happen.
    /// </summary>
    private static void PrintViews(GameState state)
    {
        Console.WriteLine("  --- what each seat is allowed to see ---");

        foreach (Seat seat in state.Seats)
        {
            PlayerView view = Projector.Project(state, seat.Index);
            Console.WriteLine();
            Console.WriteLine($"  seat {seat.Index} ({view.Seats[seat.Index].DisplayName})");
            Console.WriteLine($"    own hand   {string.Join(" ", view.Seats[seat.Index].KnownHand)}");

            string known = string.Join(
                "; ",
                view.Seats
                    .Where(other => !other.IsViewer && other.KnownHand.Count > 0)
                    .Select(other => $"seat {other.Index} holds {string.Join(" ", other.KnownHand)}"));
            Console.WriteLine($"    knows      {(known.Length > 0 ? known : "nothing about anybody else's hand")}");
            Console.WriteLine($"    treasure   {(view.Treasure is null ? "hidden" : string.Join(" ", view.Treasure))}");

            foreach (HistoryEntryView entry in view.History)
            {
                Console.WriteLine($"    {Describe(entry)}");
            }
        }
    }

    private static string Describe(HistoryEntryView entry) => entry switch
    {
        PublicQuestionView question =>
            $"question  seat {question.AskerSeat} asked seat {question.TargetSeat} "
            + $"{question.Start}->{question.End} ({question.Terrain ?? "any terrain"}) -> {question.Answer}",

        SecretQuestionView secret =>
            $"SECRET    seat {secret.AskerSeat} asked seat {secret.TargetSeat} "
            + $"{secret.Start}->{secret.End} -> {secret.Answer}",

        SecretQuestionOccurredView occurred =>
            $"secret    seat {occurred.AskerSeat} asked seat {occurred.TargetSeat} something (redacted)",

        AbilityUsedView ability => $"ability   seat {ability.SeatIndex} spent its {ability.Ability}",

        FewestTerrainAnnouncedView announced =>
            $"announce  seat {announced.SeatIndex} holds fewest {announced.Terrain}",

        TokenPassView pass =>
            $"pass      seat {pass.GiverSeat} gave {pass.Location} to seat {pass.ReceiverSeat} ({pass.Direction})",

        TokenPassOccurredView pass =>
            $"pass      seat {pass.GiverSeat} gave something to seat {pass.ReceiverSeat} "
            + $"({pass.Direction}, redacted)",

        AccusationView accusation =>
            $"accuse    seat {accusation.AccuserSeat} named {string.Join(" and ", accusation.Named)} "
            + $"({(accusation.Correct ? "correct" : "wrong")})",

        AccusationOccurredView accusation =>
            $"accuse    seat {accusation.AccuserSeat} accused wrongly and was eliminated (pair redacted)",

        GameEndedView ended =>
            $"end       {ended.Reason}"
            + (ended.WinnerSeat is { } winner ? $", seat {winner} wins" : ", nobody wins"),

        _ => $"unknown   {entry.GetType().Name}",
    };

    private static GameState Unwrap(Result<GameState> result) => result.IsSuccess
        ? result.Value
        : throw new InvalidOperationException($"The scripted game hit a rule it did not expect: {result.Error}");
}
