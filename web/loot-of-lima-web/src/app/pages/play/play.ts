import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input } from '@angular/core';
import { Terrain } from '../../core/contracts';
import { GameConnection } from '../../core/game-connection';
import { GameStore } from '../../core/game-store';
import { PlayerIdentityStore } from '../../core/player-identity';
import { ConnectionBanner } from '../../play/connection-banner';
import { HistoryPanel } from '../../play/history-panel';
import { LobbyPanel } from '../../play/lobby-panel';
import { MyHandPanel } from '../../play/my-hand-panel';
import { AskedQuestion, QuestionBuilder } from '../../play/question-builder';
import { SeatStrip } from '../../play/seat-strip';
import { SetupPanel } from '../../play/setup-panel';

/**
 * The game, from a seat.
 *
 * A composition and a set of forwarding handlers, deliberately. Every decision this page could
 * make has already been made by the server: which panel to show comes from the phase in the view,
 * what a player is allowed to do comes back as a refusal if they are not, and the answer to a
 * question is computed from a hand this client has never seen. There is nothing here to get wrong,
 * which is the point.
 */
@Component({
  selector: 'app-play',
  imports: [ConnectionBanner, HistoryPanel, LobbyPanel, MyHandPanel, QuestionBuilder, SeatStrip, SetupPanel],
  templateUrl: './play.html',
  styleUrl: './play.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Play {
  readonly roomCode = input.required<string>();

  protected readonly store = inject(GameStore);

  private readonly connection = inject(GameConnection);
  private readonly identities = inject(PlayerIdentityStore);

  /** What this browser holds for this table, if anything. Without one, the page watches. */
  private readonly identity = computed(() => this.identities.find(this.roomCode()));

  protected readonly turnHolder = computed(() => this.nameOf(this.store.activeSeat()));

  protected readonly winnerName = computed(() => this.nameOf(this.store.view()?.winnerSeat ?? null));

  constructor() {
    // The table this page is currently attached to, which is not always the one it started on.
    //
    // /play/A to /play/B is one route with a changed parameter, so this component is reused rather
    // than rebuilt and the teardown below never runs. Without clearing the store here, table A's
    // seats, hand and history stay on screen under table B's heading until B's first frame lands
    // -- and because a frame from a different game resets the high-water mark instead of being
    // compared against it, a late arrival from A's still-closing socket would be taken as current.
    let attached: string | null = null;

    effect(() => {
      const room = this.roomCode();
      const token = this.identity()?.playerToken ?? null;

      if (attached !== null && attached !== room) {
        this.store.reset();
      }
      attached = room;

      void this.connection.connect(room, token).catch(() => undefined);
    });

    inject(DestroyRef).onDestroy(() => {
      void this.connection.disconnect();
      this.store.reset();
    });
  }

  protected claim(seatIndex: number): void {
    void this.connection.claimSeat(seatIndex);
  }

  protected release(): void {
    void this.connection.releaseSeat();
  }

  protected start(scenarioId: string | null): void {
    void this.connection.startGame(scenarioId);
  }

  protected confirmTokens(): void {
    void this.connection.confirmTokensTaken();
  }

  protected confirmPass(): void {
    void this.connection.confirmPassDone();
  }

  protected announce(terrain: Terrain): void {
    void this.connection.announceFewestTerrain(terrain);
  }

  private nameOf(seatIndex: number | null): string | null {
    if (seatIndex === null) {
      return null;
    }

    return this.store.seats().find((seat) => seat.index === seatIndex)?.displayName ?? null;
  }

  protected ask(question: AskedQuestion): void {
    void this.connection.askQuestion(question.targetSeat, question.start, question.end, question.terrain);
  }
}
