import { HistoryEntryView, PlayerView, SeatView } from '../core/contracts';

/**
 * Views at a known starting point, so a store test spends its lines on the store.
 *
 * Built by hand rather than captured from the server, because these are inputs to a client that
 * must cope with whatever arrives: a fixture that could only be produced by a correct server would
 * not be able to describe a frame arriving out of order, which is the thing under test.
 */
export function seat(index: number, overrides: Partial<SeatView> = {}): SeatView {
  return {
    index,
    kind: 'Human',
    displayName: `Player ${index + 1}`,
    isViewer: false,
    isActive: false,
    eliminated: false,
    handSize: 4,
    knownHand: [],
    handIsComplete: false,
    abilitiesRemaining: ['Barrel', 'Shovel', 'Pistol'],
    pistolArmed: false,
    fewestTerrain: null,
    tokensConfirmed: false,
    passConfirmed: false,
    ...overrides,
  };
}

export function view(overrides: Partial<PlayerView> = {}): PlayerView {
  return {
    gameId: 'game-under-test',
    roomCode: 'TEST',
    phase: 'InProgress',
    playerCount: 5,
    viewerSeat: null,
    activeSeat: null,
    turnNumber: 1,
    seats: [],
    publicReveal: [],
    treasure: null,
    history: [],
    winnerSeat: null,
    endReason: null,
    version: 1,
    ...overrides,
  };
}

/** A view built for one seat, with that seat holding the given hand in full. */
export function viewFor(seatIndex: number, hand: readonly string[], overrides: Partial<PlayerView> = {}): PlayerView {
  return view({
    viewerSeat: seatIndex,
    seats: [seat(0), seat(1), seat(2)].map((existing) =>
      existing.index === seatIndex
        ? { ...existing, isViewer: true, knownHand: hand, handIsComplete: true, handSize: hand.length }
        : existing,
    ),
    ...overrides,
  });
}

/**
 * One accusation as the accuser themselves is sent it, pair and verdict included.
 *
 * A bystander is sent `accusationOccurred` instead, which has no `named` and no `correct`, so a
 * spec that wants the redacted shape has to build it rather than override its way out of this one.
 */
export function accusation(sequence: number, overrides: Partial<HistoryEntryView> = {}): HistoryEntryView {
  return {
    type: 'accusation',
    sequence,
    turnNumber: sequence,
    accuserSeat: 0,
    named: ['B1', 'F2'],
    correct: false,
    ...overrides,
  } as HistoryEntryView;
}

export function publicQuestion(sequence: number, overrides: Partial<HistoryEntryView> = {}): HistoryEntryView {
  return {
    type: 'publicQuestion',
    sequence,
    turnNumber: sequence,
    askerSeat: 0,
    targetSeat: 1,
    start: 'N',
    end: 'SE',
    terrain: null,
    answer: 2,
    ...overrides,
  } as HistoryEntryView;
}
