import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../../core/api-client';
import { AbilityType, Terrain } from '../../core/contracts';
import { GameConnection } from '../../core/game-connection';
import { GameStore } from '../../core/game-store';
import { PlayerIdentityStore } from '../../core/player-identity';
import { AbilityBar } from '../../play/ability-bar';
import { AccusationPanel, NamedTreasure } from '../../play/accusation-panel';
import { ConnectionBanner } from '../../play/connection-banner';
import { HistoryPanel } from '../../play/history-panel';
import { IslandMap } from '../../play/island-map';
import { LobbyPanel } from '../../play/lobby-panel';
import { MyHandPanel } from '../../play/my-hand-panel';
import { AskedQuestion, QuestionBuilder } from '../../play/question-builder';
import { SeatClaim, SeatStrip } from '../../play/seat-strip';
import { SetupPanel } from '../../play/setup-panel';
import { TableUi } from '../../play/table-ui';

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
  imports: [
    AbilityBar,
    AccusationPanel,
    ConnectionBanner,
    HistoryPanel,
    IslandMap,
    LobbyPanel,
    MyHandPanel,
    QuestionBuilder,
    RouterLink,
    SeatStrip,
    SetupPanel,
  ],

  // Provided here rather than at the root, so the half-built question and the half-named accusation
  // are created with this page and die with it. Neither is anything to carry to the next table.
  providers: [TableUi],
  templateUrl: './play.html',
  styleUrl: './play.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Play {
  readonly roomCode = input.required<string>();

  protected readonly store = inject(GameStore);

  private readonly api = inject(ApiClient);
  private readonly connection = inject(GameConnection);
  private readonly identities = inject(PlayerIdentityStore);

  /** What this browser holds for this table, if anything. Without one, the page watches. */
  private readonly identity = computed(() => this.identities.find(this.roomCode()));

  /**
   * Whether the seat strip has to ask who this is before it can offer a seat.
   *
   * The QR code opens the table now, so the common case on this page is a browser holding no token
   * at all — which used to be the one state where a seat could not be taken. It is now the state
   * where taking one costs a name first.
   */
  protected readonly needsName = computed(() => this.identity() === null);

  protected readonly turnHolder = computed(() => this.nameOf(this.store.activeSeat()));

  protected readonly winnerName = computed(() => this.nameOf(this.store.view()?.winnerSeat ?? null));

  protected readonly eliminated = computed(() => this.store.mySeat()?.eliminated === true);

  /**
   * Who gave up, for a screen that is not theirs.
   *
   * `Conceded` carries no seat — `GiveUp` finishes with no winner — but it does not need to: it is
   * reachable only at a table of one, so the single human seat is the player who conceded. A count
   * of a field every viewer is sent in full, like `isSolo` beside it, and not a deduction.
   *
   * Falls back to naming nobody rather than to naming the wrong person, which is what a bare
   * `displayName` off the first seat would risk if this were ever reached another way.
   */
  protected readonly concededBy = computed(() => {
    const humans = this.store.seats().filter((seat) => seat.kind === 'Human');
    return humans.length === 1 ? humans[0].displayName : 'The player';
  });

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

  /**
   * Takes a seat, joining the table first if this browser has not joined it.
   *
   * Two steps, in this order, and they cannot be one: `POST /join` is what issues the token, and
   * `ClaimSeat` is a hub command the *connection* has to be carrying that token for. The socket
   * open at this moment is a spectator's, so it is replaced in between. The effect below would do
   * that too, since it watches the identity — but not until the next tick, which is after the
   * claim would have gone out and come back refused. Both calls land on the same queue inside
   * `GameConnection`, so whichever runs second finds the connection already made and returns.
   *
   * A name is required exactly when `needsName` said so. Trimmed and re-checked here rather than
   * trusted from the strip: this is the call that puts it in front of the whole table.
   */
  protected async claim(request: SeatClaim): Promise<void> {
    if (this.identity() === null) {
      const name = request.name?.trim() ?? '';
      if (name.length === 0) {
        return;
      }

      try {
        const joined = await this.api.join(this.roomCode(), name);
        const identity = this.identities.remember(this.roomCode(), joined, name);
        await this.connection.connect(this.roomCode(), identity.playerToken);
      } catch {
        // The same channel every refusal from the hub uses, so there is one place on this page
        // where a player finds out something did not happen.
        this.store.noteError({ code: 'JoinFailed', message: `Could not take a seat at table ${this.roomCode()}.` });
        return;
      }
    }

    await this.connection.claimSeat(request.seatIndex);
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

  protected spendAbility(ability: AbilityType): void {
    void this.connection.spendAbility(ability);
  }

  /**
   * Sends the question the builder assembled, secretly or in the open.
   *
   * Two hub methods rather than one with a flag, because they produce two different history
   * entries and only one of them requires a Pistol spent this turn. The two requests carry the
   * same fields; what differs is who is ever allowed to read the answer.
   */
  protected ask(question: AskedQuestion): void {
    if (question.secret) {
      void this.connection.askSecretQuestion(
        question.targetSeat,
        question.start,
        question.end,
        question.terrain,
      );
      return;
    }

    void this.connection.askQuestion(question.targetSeat, question.start, question.end, question.terrain);
  }

  protected accuse(named: NamedTreasure): void {
    void this.connection.accuse(named.first, named.second);
  }

  protected giveUp(): void {
    void this.connection.giveUp();
  }

  private nameOf(seatIndex: number | null): string | null {
    if (seatIndex === null) {
      return null;
    }

    return this.store.seats().find((seat) => seat.index === seatIndex)?.displayName ?? null;
  }
}
