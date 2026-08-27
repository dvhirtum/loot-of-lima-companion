import { Injectable, computed, signal } from '@angular/core';
import { ErrorDto, HistoryEntryView, PlayerView, SeatView } from './contracts';

/** How this client is getting on with the hub, for the banner that says so. */
export type ConnectionStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/**
 * The one place game state lives on the client.
 *
 * It is a holder, not a reducer. The server sends a complete `PlayerView` on every change, already
 * projected for this viewer, so there is nothing here to merge and nothing here to derive: the
 * whole job is to keep the newest view and hand slices of it to components. That is deliberate --
 * a client that computed game state would be a second implementation of the rules, and a second
 * chance to compute something it was never told.
 */
@Injectable({ providedIn: 'root' })
export class GameStore {
  private readonly _view = signal<PlayerView | null>(null);
  private readonly _status = signal<ConnectionStatus>('idle');
  private readonly _error = signal<ErrorDto | null>(null);
  private readonly _stale = signal(0);

  /**
   * The highest version accepted so far.
   *
   * Kept separately from `_view` rather than read back off it, because they answer different
   * questions: `_view` is what is on screen, this is the high-water mark that says what is too old
   * to put there. They only ever differ for the instant between a frame being judged and being
   * stored, but keeping the mark in its own place means the rule cannot be quietly broken by
   * something that assigns to `_view`.
   */
  private highestVersion = -1;

  /** The newest view this client is entitled to, or null before the first frame arrives. */
  readonly view = this._view.asReadonly();

  readonly status = this._status.asReadonly();

  /** The last refusal the server addressed to this client. Errors are never broadcast. */
  readonly error = this._error.asReadonly();

  /**
   * How many frames have been dropped for arriving late.
   *
   * Exposed because a silent guard is a guard nobody can tell has stopped working. A test can
   * assert it moves, and a developer watching it climb during normal play would be looking at a
   * real problem rather than a healthy race.
   */
  readonly staleFramesDropped = this._stale.asReadonly();

  readonly roomCode = computed(() => this._view()?.roomCode ?? null);

  readonly phase = computed(() => this._view()?.phase ?? null);

  readonly turnNumber = computed(() => this._view()?.turnNumber ?? 0);

  readonly seats = computed<readonly SeatView[]>(() => this._view()?.seats ?? []);

  readonly activeSeat = computed(() => this._view()?.activeSeat ?? null);

  /** The viewer's own seat, or null when watching without one. */
  readonly mySeat = computed<SeatView | null>(() => {
    const view = this._view();
    if (view === null || view.viewerSeat === null) {
      return null;
    }

    return view.seats.find((seat) => seat.index === view.viewerSeat) ?? null;
  });

  readonly isSpectator = computed(() => this._view() !== null && this._view()!.viewerSeat === null);

  /**
   * The viewer's own locations. Complete by definition: the only hand the server ever sends in
   * full is the viewer's own, so this is the one place `knownHand` can be read as a whole hand.
   */
  readonly myHand = computed<readonly string[]>(() => this.mySeat()?.knownHand ?? []);

  readonly isMyTurn = computed(() => {
    const seat = this.mySeat();
    return seat !== null && seat.isActive;
  });

  /** Newest first, which is the order a table reads a log in. */
  readonly history = computed<readonly HistoryEntryView[]>(() =>
    [...(this._view()?.history ?? [])].sort((left, right) => right.sequence - left.sequence),
  );

  readonly treasure = computed<readonly string[] | null>(() => this._view()?.treasure ?? null);

  readonly isFinished = computed(() => this._view()?.phase === 'Finished');

  /**
   * One human at the table, the rest bots — the shape the printed scenarios are written for.
   *
   * A count of a field every viewer is already sent in full, not a deduction: who is a bot is
   * public from the lobby onwards, and it is on the seat strip in words. It decides what this page
   * says about a wrong accusation, and nothing else; the engine still rules on every command that
   * turns on it.
   */
  readonly isSolo = computed(() => this.seats().filter((seat) => seat.kind === 'Human').length === 1);

  /**
   * How many times this viewer has accused and been wrong.
   *
   * Read from their own accusations, which is the only kind they are sent in full: somebody else's
   * failed accusation arrives as `accusationOccurred`, with no pair and no seat to attribute it to
   * beyond the accuser. Zero for a spectator, who has never accused anything.
   */
  readonly myWrongAccusations = computed(() => {
    const seat = this.mySeat();
    if (seat === null) {
      return 0;
    }

    return this.history().filter(
      (entry) => entry.type === 'accusation' && entry.accuserSeat === seat.index && !entry.correct,
    ).length;
  });

  /**
   * Takes a frame, unless a newer one has already been taken.
   *
   * Commands are serialised by the server, but the sends that follow them are not: each frame is a
   * separate await over a separate connection, so one can overtake its successor on the way out.
   * Every frame carries the revision it was built from, and keeping the highest seen is what makes
   * that harmless.
   *
   * The comparison is strict. A frame at the version already held is not stale, and it is not
   * always a duplicate either: rejoining with a token projects the same revision to a seat rather
   * than to a spectator, so the second frame carries a hand the first one could not. Dropping it
   * would leave a player who has just sat down staring at somebody else's view of the table.
   *
   * A frame from a different game resets the mark instead of being compared against it. Versions
   * count per game, so a new table starting at 1 would otherwise be judged stale by the last table
   * this browser watched.
   *
   * @returns whether the frame was taken.
   */
  apply(view: PlayerView): boolean {
    const current = this._view();
    if (current !== null && current.gameId === view.gameId && view.version < this.highestVersion) {
      this._stale.update((dropped) => dropped + 1);
      return false;
    }

    this.highestVersion = view.version;
    this._view.set(view);
    return true;
  }

  setStatus(status: ConnectionStatus): void {
    this._status.set(status);
  }

  noteError(error: ErrorDto): void {
    this._error.set(error);
  }

  clearError(): void {
    this._error.set(null);
  }

  /**
   * Forgets everything, including the high-water mark.
   *
   * Called when this client stops watching a game rather than when it merely loses the connection:
   * a reconnect to the same game must keep the mark, or the first frame back could be an older one
   * this store has already shown and would happily show again.
   */
  reset(): void {
    this.highestVersion = -1;
    this._view.set(null);
    this._error.set(null);
    this._stale.set(0);
    this._status.set('idle');
  }
}
