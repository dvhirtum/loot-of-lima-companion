import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  output,
  signal,
} from '@angular/core';
import { DIRECTIONS, Direction, TERRAINS, Terrain } from '../core/contracts';
import { GameStore } from '../core/game-store';
import { TableUi } from './table-ui';

/** A question, as the player has built it so far. */
export interface AskedQuestion {
  readonly targetSeat: number;
  readonly start: Direction;
  readonly end: Direction;
  readonly terrain: Terrain | null;
  /** A Pistol question. Only the asker and the target ever learn the range or the answer. */
  readonly secret: boolean;
}

/**
 * Pick a seat, two compass directions and optionally a terrain.
 *
 * There is deliberately nowhere here to enter a roll. The dice stay on the table and the app never
 * asks what came up: that removes an error-prone data-entry step from every single turn, at the
 * price of not validating that a question was rollable. Players self-police it exactly as they do
 * at the table, and the app trusts them exactly as the table does.
 *
 * Spending the Pistol turns this panel into the secret one, because that is what spending it is
 * for. Nothing else about the panel changes: the ability lifts the roll, not the shapes a question
 * may take, so the terrain field stays exactly where it was and means exactly what it meant.
 */
@Component({
  selector: 'app-question-builder',
  templateUrl: './question-builder.html',
  styles: `
    .builder {
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
      align-items: end;
    }

    .reading {
      margin: 0;
      font-family: var(--font-display);
      font-size: 1.05rem;
    }

    .reading--secret {
      color: var(--coral);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuestionBuilder {
  protected readonly store = inject(GameStore);

  private readonly ui = inject(TableUi);

  protected readonly directions = DIRECTIONS;

  protected readonly terrains = TERRAINS;

  readonly ask = output<AskedQuestion>();

  /**
   * Ephemeral: it belongs to this half-built question and to nothing else.
   *
   * The plan puts the in-progress question in component state on purpose. The store holds what the
   * server said, and a half-chosen range is not something the server has said anything about. The
   * one part that leaves this component is the range itself, which the island map draws -- and that
   * goes through `TableUi`, which is ephemeral in the same way and dies with the page.
   */
  protected readonly targetSeat = signal<number | null>(null);
  protected readonly start = signal<Direction>('N');
  protected readonly end = signal<Direction>('N');
  protected readonly terrain = signal<Terrain | null>(null);

  /** Everybody but the viewer. An eliminated player still answers, so they stay on the list. */
  protected readonly targets = computed(() => this.store.seats().filter((seat) => !seat.isViewer));

  /**
   * Whether this turn's question is a Pistol one.
   *
   * Read off the view rather than worked out from the ability history: the server projects it, so
   * the client is told rather than deriving. `AskSecretQuestion` is refused unless the Pistol was
   * spent on this very turn, and this is the same fact the server will check.
   */
  protected readonly secret = computed(() => this.store.mySeat()?.pistolArmed === true);

  protected readonly canAsk = computed(
    () => this.store.phase() === 'InProgress' && this.store.isMyTurn() && this.chosenTarget() !== null,
  );

  /**
   * What the range means, spelled out.
   *
   * The arc runs clockwise from the start, includes it, and stops before the end -- the dial's
   * arrows point at the compass lines rather than at sectors, and tokens sit in the gaps between
   * the lines. Coincident arrows sweep the whole circle. It is the subtle part of the game, so the
   * app says it in words rather than leaving a player to infer it from an answer that surprised them.
   */
  protected readonly reading = computed(() => {
    const scope = this.terrain() === null ? 'locations' : `${this.terrain()!.toLowerCase()} locations`;
    const arc =
      this.start() === this.end()
        ? 'anywhere on the island'
        : `clockwise from ${this.start()} up to, but not including, ${this.end()}`;

    return `How many ${scope} ${arc}?`;
  });

  constructor() {
    // The map draws the range as it is chosen, which is the whole reason there is a map rather than
    // a picture of one. Only the range goes across: no target, no answer, nothing about a hand.
    effect(() => {
      this.ui.setHighlight({
        start: this.start(),
        end: this.end(),
        terrain: this.terrain(),
        secret: this.secret(),
      });
    });

    // The panel is only on screen while it is this seat's turn, so leaving a wedge behind would
    // leave the map showing a question nobody is asking any more.
    inject(DestroyRef).onDestroy(() => this.ui.setHighlight(null));
  }

  protected chooseTarget(value: string): void {
    this.targetSeat.set(value === '' ? null : Number(value));
  }

  protected chooseTerrain(value: string): void {
    this.terrain.set(value === '' ? null : (value as Terrain));
  }

  protected submit(): void {
    const target = this.chosenTarget();
    if (target === null) {
      return;
    }

    this.ask.emit({
      targetSeat: target,
      start: this.start(),
      end: this.end(),
      terrain: this.terrain(),
      secret: this.secret(),
    });
  }

  /**
   * The chosen seat, but only while it is still a seat somebody could be asked about.
   *
   * A seat chosen and then vacated would otherwise stay selected and be sent, and the refusal that
   * came back would name a seat no longer on screen.
   */
  private readonly chosenTarget = computed(() => {
    const chosen = this.targetSeat();
    return chosen !== null && this.targets().some((seat) => seat.index === chosen) ? chosen : null;
  });
}
