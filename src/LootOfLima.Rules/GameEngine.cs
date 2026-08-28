namespace LootOfLima.Rules;

/// <summary>
/// Every legal move, as a pure function from one <see cref="GameState"/> to the next.
/// </summary>
/// <remarks>
/// Rule violations come back as a <see cref="GameError"/> inside the result; only programming
/// errors throw. Nothing here reads a clock, a random number generator or the outside world, so a
/// game is entirely reproducible from its seed and the sequence of commands applied to it.
/// </remarks>
public static class GameEngine
{
    /// <summary>A game in the lobby, with its seats laid out but nothing dealt.</summary>
    public static Result<GameState> CreateGame(
        string gameId,
        string roomCode,
        IReadOnlyList<SeatSpec> seats,
        int randomSeed)
    {
        ArgumentNullException.ThrowIfNull(seats);

        if (!DealTable.IsSupportedPlayerCount(seats.Count))
        {
            return new GameError(
                GameErrorCode.UnsupportedPlayerCount,
                $"Loot of Lima is for {DealTable.MinPlayers}-{DealTable.MaxPlayers} players, not {seats.Count}.");
        }

        return Result<GameState>.Success(new GameState(
            GameId: gameId,
            RoomCode: roomCode,
            Phase: GamePhase.Lobby,
            Seats: [.. seats.Select((spec, index) => Seat.Create(index, spec))],
            Deal: null,
            ActiveSeat: null,
            TurnNumber: 0,
            History: [],
            PendingPasses: [],
            WinnerSeat: null,
            EndReason: null,
            RandomSeed: randomSeed));
    }

    /// <summary>Turns an open seat into a human one. Only in the lobby.</summary>
    public static Result<GameState> ClaimSeat(GameState state, int seatIndex, string playerId, string displayName)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if ((Reject(state, GamePhase.Lobby) ?? RejectSeat(state, seatIndex)) is { } error)
        {
            return error;
        }

        Seat seat = state.SeatAt(seatIndex);
        if (seat.IsHuman)
        {
            return new GameError(
                GameErrorCode.SeatAlreadyClaimed, $"Seat {seatIndex} is already taken by {seat.DisplayName}.");
        }

        // One identity, one hand. Without this a single player could hold two seats and the
        // projector would build a full-hand view of each for them, which is the whole
        // hidden-information design undone by opening a second browser tab.
        Seat? held = state.HumanSeats.FirstOrDefault(
            other => string.Equals(other.PlayerId, playerId, StringComparison.Ordinal));
        if (held is not null)
        {
            return new GameError(
                GameErrorCode.PlayerAlreadySeated, $"{playerId} already holds seat {held.Index}.");
        }

        return Result<GameState>.Success(WithSeat(
            state,
            seat with { Kind = SeatKind.Human, PlayerId = playerId, DisplayName = displayName }));
    }

    /// <summary>Hands a seat back. It becomes a bot again, so the table always stays full.</summary>
    public static Result<GameState> ReleaseSeat(GameState state, int seatIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if ((Reject(state, GamePhase.Lobby) ?? RejectSeat(state, seatIndex)) is { } error)
        {
            return error;
        }

        Seat seat = state.SeatAt(seatIndex);
        if (!seat.IsHuman)
        {
            return new GameError(GameErrorCode.SeatNotClaimed, $"Seat {seatIndex} is not claimed by anybody.");
        }

        return Result<GameState>.Success(WithSeat(
            state,
            seat with { Kind = SeatKind.Bot, PlayerId = null, DisplayName = $"Bot {seatIndex + 1}" }));
    }

    /// <summary>
    /// Deals, and asks every human to pick up their tokens. The deal is supplied rather than made
    /// here so that a scenario deal and a generated one enter the engine by exactly the same door.
    /// </summary>
    public static Result<GameState> Start(GameState state, Deal deal, IShuffleSource shuffle)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(deal);
        ArgumentNullException.ThrowIfNull(shuffle);

        if (Reject(state, GamePhase.Lobby) is { } error)
        {
            return error;
        }

        if (deal.PlayerCount != state.PlayerCount)
        {
            return new GameError(
                GameErrorCode.DealDoesNotFitGame,
                $"The deal is for {deal.PlayerCount} players but the game has {state.PlayerCount} seats.");
        }

        if (!state.HumanSeats.Any())
        {
            return new GameError(GameErrorCode.NoHumanSeats, "A game needs at least one human seat to be played.");
        }

        bool passing = Passing.IsRequired(state.PlayerCount, deal.Source.Kind);

        // Bots have no screen to confirm on, so they are finished before they begin — and so is
        // everybody at a table that does no passing. Leaving a human unconfirmed in a game that
        // never reaches Passing would leave the client unable to tell "still owes a token" from
        // "nothing to hand over", because the flag could no longer be flipped by anything.
        IReadOnlyList<Seat> seats =
        [
            .. state.Seats.Select(seat => seat with
            {
                Hand = deal.Hands[seat.Index],
                TokensConfirmed = !seat.IsHuman,
                PassConfirmed = !seat.IsHuman || !passing,
            }),
        ];

        IReadOnlyList<PassRecord> passes = passing ? Passing.Plan(deal.Hands, shuffle) : [];

        return Result<GameState>.Success(state with
        {
            Phase = GamePhase.Dealing,
            Seats = seats,
            Deal = deal,
            PendingPasses = passes,
        });
    }

    /// <summary>The physical-versus-digital gate: this player holds the tokens the app dealt them.</summary>
    public static Result<GameState> ConfirmTokensTaken(GameState state, int seatIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if ((Reject(state, GamePhase.Dealing) ?? RejectHumanSeat(state, seatIndex)) is { } error)
        {
            return error;
        }

        Seat seat = state.SeatAt(seatIndex);
        if (seat.TokensConfirmed)
        {
            return new GameError(GameErrorCode.AlreadyConfirmed, $"Seat {seatIndex} already took its tokens.");
        }

        GameState next = WithSeat(state, seat with { TokensConfirmed = true });
        if (next.HumanSeats.Any(other => !other.TokensConfirmed))
        {
            return Result<GameState>.Success(next);
        }

        return Result<GameState>.Success(next.PendingPasses.Count > 0 ? BeginPassing(next) : BeginAnnouncing(next));
    }

    /// <summary>The same gate for passing: this player has handed their token over.</summary>
    public static Result<GameState> ConfirmPassDone(GameState state, int seatIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if ((Reject(state, GamePhase.Passing) ?? RejectHumanSeat(state, seatIndex)) is { } error)
        {
            return error;
        }

        Seat seat = state.SeatAt(seatIndex);
        if (seat.PassConfirmed)
        {
            return new GameError(GameErrorCode.AlreadyConfirmed, $"Seat {seatIndex} already passed its token.");
        }

        GameState next = WithSeat(state, seat with { PassConfirmed = true });
        return Result<GameState>.Success(
            next.HumanSeats.Any(other => !other.PassConfirmed) ? next : BeginAnnouncing(next));
    }

    /// <summary>
    /// Announces the terrain this seat holds fewest of, in turn order, before play begins.
    /// </summary>
    /// <remarks>
    /// Holding none of a terrain counts as fewest. Where two terrains tie the choice is the
    /// player's, so the engine checks only that the announcement is truthful and records nothing
    /// about how many terrains were eligible — the rulebook says the tie itself stays hidden.
    /// </remarks>
    public static Result<GameState> AnnounceFewestTerrain(GameState state, int seatIndex, Terrain terrain)
    {
        ArgumentNullException.ThrowIfNull(state);

        if ((Reject(state, GamePhase.Announcing) ?? RejectHumanSeat(state, seatIndex)) is { } error)
        {
            return error;
        }

        if (state.ActiveSeat != seatIndex)
        {
            return new GameError(
                GameErrorCode.NotYourTurn,
                $"Seat {state.ActiveSeat} announces before seat {seatIndex} does.");
        }

        Seat seat = state.SeatAt(seatIndex);
        if (!FewestTerrainRule.IsTruthful(seat.Hand, terrain))
        {
            return new GameError(
                GameErrorCode.NotAFewestTerrain,
                $"Seat {seatIndex} does not hold {terrain} fewest of.");
        }

        return Result<GameState>.Success(AdvanceAnnouncing(Announce(state, seat, terrain)));
    }

    /// <summary>
    /// Spends one ability token. For Barrel and Shovel that is the entire effect, because the app
    /// does not model the dice they act on. Pistol also unlocks a secret question for this turn.
    /// </summary>
    public static Result<GameState> SpendAbility(GameState state, int seatIndex, AbilityType ability)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (RejectTurnHolder(state, seatIndex) is { } error)
        {
            return error;
        }

        Seat seat = state.SeatAt(seatIndex);
        if (!seat.HasAbility(ability))
        {
            return new GameError(
                GameErrorCode.AbilityAlreadySpent, $"Seat {seatIndex} has already spent its {ability}.");
        }

        Seat spent = seat with
        {
            AbilitiesRemaining = [.. seat.AbilitiesRemaining.Where(remaining => remaining != ability)],
            PistolArmedTurn = ability == AbilityType.Pistol ? state.TurnNumber : seat.PistolArmedTurn,
        };

        return Result<GameState>.Success(WithHistory(
            WithSeat(state, spent),
            sequence => new AbilityUsedEntry(sequence, state.TurnNumber, seatIndex, ability)));
    }

    /// <summary>
    /// Asks a question and answers it from the hand the app dealt, for bots and humans alike. The
    /// range is not checked against any roll: the app does not model the dice, by design.
    /// </summary>
    public static Result<GameState> AskQuestion(
        GameState state,
        int askerSeat,
        int targetSeat,
        QueryRange range,
        BoardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(layout);

        if (RejectQuestion(state, askerSeat, targetSeat) is { } error)
        {
            return error;
        }

        int answer = Engine.CountInRange(state.SeatAt(targetSeat).Hand, range, layout);

        return Result<GameState>.Success(AdvanceTurn(WithHistory(
            state,
            sequence => new PublicQuestionEntry(sequence, state.TurnNumber, askerSeat, targetSeat, range, answer))));
    }

    /// <summary>
    /// Asks a question only the two participants ever see. Requires a Pistol spent this turn, and
    /// takes the same range an ordinary question takes, terrain filter included.
    /// </summary>
    /// <remarks>
    /// The Pistol frees the asker from the dice they rolled, not from the shapes a question may
    /// take: anything a roll could theoretically have allowed is allowed here, and a single terrain
    /// is one of those. So there is nothing extra to enforce, and the reason ordinary question
    /// legality goes unchecked — the app does not model the roll — applies here unchanged.
    /// </remarks>
    public static Result<GameState> AskSecretQuestion(
        GameState state,
        int askerSeat,
        int targetSeat,
        QueryRange range,
        BoardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(layout);

        if (RejectQuestion(state, askerSeat, targetSeat) is { } error)
        {
            return error;
        }

        if (state.SeatAt(askerSeat).PistolArmedTurn != state.TurnNumber)
        {
            return new GameError(
                GameErrorCode.PistolNotSpentThisTurn,
                $"Seat {askerSeat} must spend its Pistol this turn before asking a secret question.");
        }

        int answer = Engine.CountInRange(state.SeatAt(targetSeat).Hand, range, layout);

        return Result<GameState>.Success(AdvanceTurn(WithHistory(
            state,
            sequence => new SecretQuestionEntry(sequence, state.TurnNumber, askerSeat, targetSeat, range, answer))));
    }

    /// <summary>
    /// Names the two treasure locations. Legal at any moment, including off-turn. Right wins the
    /// game; wrong eliminates the accuser, reveals nothing, and leaves them answering questions.
    /// </summary>
    public static Result<GameState> Accuse(GameState state, int accuserSeat, LocationId first, LocationId second)
    {
        ArgumentNullException.ThrowIfNull(state);

        if ((Reject(state, GamePhase.InProgress) ?? RejectHumanSeat(state, accuserSeat)) is { } error)
        {
            return error;
        }

        Seat accuser = state.SeatAt(accuserSeat);
        if (accuser.Eliminated)
        {
            return new GameError(
                GameErrorCode.SeatIsEliminated, $"Seat {accuserSeat} is eliminated and can no longer accuse.");
        }

        if (!first.IsValid || !second.IsValid || first == second)
        {
            return new GameError(
                GameErrorCode.InvalidAccusation,
                "An accusation names two different locations from the 24 on the island.");
        }

        IReadOnlyList<LocationId> named = [.. new[] { first, second }.Order()];
        bool correct = named.SequenceEqual(state.Deal!.Treasure);

        GameState next = WithHistory(
            state,
            sequence => new AccusationEntry(sequence, state.TurnNumber, accuserSeat, named, correct));

        if (correct)
        {
            return Result<GameState>.Success(Finish(next, accuserSeat, GameEndReason.CorrectAccusation));
        }

        // A table of one is the exception, and it is not a house rule so much as a refusal to make
        // the penalty meaningless. Eliminating the only player finishes the game, and finishing the
        // game is the single moment the treasure is projected -- so the punishment for guessing
        // wrong would be being handed the answer. The scenario books are puzzles; getting one wrong
        // is how you learn the pair is ruled out, which is a real deduction and the player's own.
        // So nothing is revealed, the seat stays in, and the turn ends the way any other action
        // taken on your own turn ends it.
        if (state.IsSolo)
        {
            return Result<GameState>.Success(next.ActiveSeat == accuserSeat ? AdvanceTurn(next) : next);
        }

        next = WithSeat(next, accuser with { Eliminated = true });

        // At least two humans sat down, because a solo table returned above, and that is what makes
        // one survivor decisive rather than ambiguous. It also means the count can never reach zero
        // here: the game ends the first time it falls to one, so there is never a second-to-last
        // elimination left to make.
        if (next.ActiveHumanSeats.Count() == 1)
        {
            return Result<GameState>.Success(Finish(
                next, next.ActiveHumanSeats.Single().Index, GameEndReason.LastPlayerStanding));
        }

        // Being eliminated ends your turn, but only your turn.
        return Result<GameState>.Success(next.ActiveSeat == accuserSeat ? AdvanceTurn(next) : next);
    }

    /// <summary>
    /// Ends a solo game the only way it can now end short of a correct accusation: the player asks
    /// to be shown the treasure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists because <see cref="Accuse"/> no longer finishes a solo game, so without it a
    /// player who cannot solve the puzzle has no way to find out what the answer was.
    /// </para>
    /// <para>
    /// It is refused until that seat has accused and been wrong at least once, and that is a rule
    /// rather than a nicety about where a button appears. The treasure is the one thing this whole
    /// application is built to withhold, and a command that hands it over on request is one misclick
    /// away from spoiling a puzzle somebody was halfway through. Requiring a wrong accusation first
    /// means giving up costs what giving up costs: you have to have committed to an answer and
    /// found out it was not the answer.
    /// </para>
    /// </remarks>
    public static Result<GameState> GiveUp(GameState state, int seatIndex)
    {
        ArgumentNullException.ThrowIfNull(state);

        if ((Reject(state, GamePhase.InProgress) ?? RejectHumanSeat(state, seatIndex)) is { } error)
        {
            return error;
        }

        if (!state.IsSolo)
        {
            return new GameError(
                GameErrorCode.NotASoloGame,
                "Only a table of one player can give up. Anywhere else, accusing wrongly is how you leave a game.");
        }

        // Named by seat although the table has exactly one human, so the two readings cannot differ
        // today. Written this way because the rule being expressed is about the player giving up
        // rather than about the table, and a check that reads the rule cannot drift away from it.
        bool hasAccusedWrongly = state.History
            .OfType<AccusationEntry>()
            .Any(entry => entry.AccuserSeat == seatIndex && !entry.Correct);

        if (!hasAccusedWrongly)
        {
            return new GameError(
                GameErrorCode.NothingToGiveUpOn,
                $"Seat {seatIndex} has not accused and been wrong yet, so there is nothing to give up on.");
        }

        return Result<GameState>.Success(Finish(state, null, GameEndReason.Conceded));
    }

    /// <summary>
    /// Moves the planned tokens and tells their two endpoints, then waits for every human to say
    /// the physical tokens have changed hands.
    /// </summary>
    private static GameState BeginPassing(GameState state)
    {
        IReadOnlyList<IReadOnlyList<LocationId>> passed =
            Passing.Apply([.. state.Seats.Select(seat => seat.Hand)], state.PendingPasses);

        GameState next = state with
        {
            Phase = GamePhase.Passing,
            Seats = [.. state.Seats.Select(seat => seat with { Hand = passed[seat.Index] })],
            PendingPasses = [],
        };

        foreach (PassRecord pass in state.PendingPasses)
        {
            next = WithHistory(next, sequence => new TokenPassEntry(sequence, 0, pass));
        }

        return next;
    }

    private static GameState BeginAnnouncing(GameState state) =>
        AdvanceAnnouncing(state with { Phase = GamePhase.Announcing, ActiveSeat = null });

    /// <summary>
    /// Walks the seats in order, settling every announcement that needs no human, and stops at the
    /// first seat that does. When none is left, play begins.
    /// </summary>
    /// <remarks>
    /// Turn order is enforced rather than treated as a formality: a player breaking a tie has heard
    /// everybody before them, so who announces when can genuinely change what they choose.
    /// </remarks>
    private static GameState AdvanceAnnouncing(GameState state)
    {
        GameState next = state;

        foreach (Seat seat in state.Seats)
        {
            if (next.SeatAt(seat.Index).FewestTerrain is not null)
            {
                continue;
            }

            if (PrescribedFor(next, seat.Index) is { } prescribed)
            {
                next = Announce(next, next.SeatAt(seat.Index), prescribed);
                continue;
            }

            if (seat.IsHuman)
            {
                return next with { ActiveSeat = seat.Index };
            }

            next = Announce(next, next.SeatAt(seat.Index), ChooseFewestTerrain(next, next.SeatAt(seat.Index)));
        }

        return BeginPlay(next);
    }

    private static Terrain? PrescribedFor(GameState state, int seatIndex) =>
        state.Deal?.PrescribedFewestTerrain is { } prescribed ? prescribed[seatIndex] : null;

    /// <summary>Breaks a bot's tie.</summary>
    /// <remarks>
    /// Deliberately not the first tied terrain in declaration order. A fixed order would tell
    /// everybody that the terrains before the announced one are strictly more common, which is more
    /// than the rule reveals — and the rule says a tie must stay hidden. Derived from the game's
    /// seed, which is never projected, so the choice stays reproducible without being inferable.
    /// </remarks>
    private static Terrain ChooseFewestTerrain(GameState state, Seat seat)
    {
        IReadOnlyList<Terrain> minima = FewestTerrainRule.Minima(seat.Hand);
        if (minima.Count == 1)
        {
            return minima[0];
        }

        IShuffleSource source = new DeterministicShuffleSource(unchecked((state.RandomSeed * 397) ^ seat.Index));
        return minima[source.Next(minima.Count)];
    }

    private static GameState Announce(GameState state, Seat seat, Terrain terrain) => WithHistory(
        WithSeat(state, seat with { FewestTerrain = terrain }),
        sequence => new FewestTerrainAnnouncedEntry(sequence, 0, seat.Index, terrain));

    private static GameState BeginPlay(GameState state) => state with
    {
        Phase = GamePhase.InProgress,
        TurnNumber = 1,
        ActiveSeat = state.ActiveHumanSeats.First().Index,
    };

    /// <summary>Turn order is human seats only, ascending and wrapping, skipping eliminated ones.</summary>
    private static GameState AdvanceTurn(GameState state)
    {
        int from = state.ActiveSeat ?? -1;
        for (int step = 1; step <= state.PlayerCount; step++)
        {
            Seat candidate = state.Seats[(from + step) % state.PlayerCount];
            if (candidate.IsHuman && !candidate.Eliminated)
            {
                return state with { TurnNumber = state.TurnNumber + 1, ActiveSeat = candidate.Index };
            }
        }

        return Finish(state, null, GameEndReason.AllPlayersEliminated);
    }

    private static GameState Finish(GameState state, int? winnerSeat, GameEndReason reason) => WithHistory(
        state with
        {
            Phase = GamePhase.Finished,
            ActiveSeat = null,
            WinnerSeat = winnerSeat,
            EndReason = reason,
        },
        sequence => new GameEndedEntry(sequence, state.TurnNumber, winnerSeat, reason));

    private static GameState WithSeat(GameState state, Seat seat) => state with
    {
        Seats = [.. state.Seats.Select(other => other.Index == seat.Index ? seat : other)],
    };

    private static GameState WithHistory(GameState state, Func<int, HistoryEntry> entry) => state with
    {
        History = [.. state.History, entry(state.History.Count)],
    };

    private static GameError? Reject(GameState state, GamePhase required) => state.Phase == required
        ? null
        : new GameError(GameErrorCode.WrongPhase, $"That is not allowed while the game is {state.Phase}.");

    private static GameError? RejectSeat(GameState state, int seatIndex) => state.IsSeatIndex(seatIndex)
        ? null
        : new GameError(GameErrorCode.SeatOutOfRange, $"This game has no seat {seatIndex}.");

    private static GameError? RejectHumanSeat(GameState state, int seatIndex)
    {
        if (RejectSeat(state, seatIndex) is { } error)
        {
            return error;
        }

        return state.SeatAt(seatIndex).IsHuman
            ? null
            : new GameError(GameErrorCode.SeatIsNotHuman, $"Seat {seatIndex} is a bot, and bots only answer.");
    }

    private static GameError? RejectTurnHolder(GameState state, int seatIndex)
    {
        if ((Reject(state, GamePhase.InProgress) ?? RejectHumanSeat(state, seatIndex)) is { } error)
        {
            return error;
        }

        if (state.SeatAt(seatIndex).Eliminated)
        {
            return new GameError(
                GameErrorCode.SeatIsEliminated, $"Seat {seatIndex} is eliminated and takes no more turns.");
        }

        return state.ActiveSeat == seatIndex
            ? null
            : new GameError(GameErrorCode.NotYourTurn, $"It is seat {state.ActiveSeat} to play, not seat {seatIndex}.");
    }

    private static GameError? RejectQuestion(GameState state, int askerSeat, int targetSeat)
    {
        if ((RejectTurnHolder(state, askerSeat) ?? RejectSeat(state, targetSeat)) is { } error)
        {
            return error;
        }

        return askerSeat == targetSeat
            ? new GameError(GameErrorCode.CannotTargetYourself, "You cannot ask yourself a question.")
            : null;
    }
}
