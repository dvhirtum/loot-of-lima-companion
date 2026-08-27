import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { DIRECTIONS, Direction, TERRAINS, Terrain } from '../core/contracts';
import { GameStore } from '../core/game-store';

/** A question, as the player has built it so far. */
export interface AskedQuestion {
  readonly targetSeat: number;
  readonly start: Direction;
  readonly end: Direction;
  readonly terrain: Terrain | null;
}

/**
 * Pick a seat, two compass directions and optionally a terrain.
 *
 * There is deliberately nowhere here to enter a roll. The dice stay on the table and the app never
 * asks what came up: that removes an error-prone data-entry step from every single turn, at the
 * price of not validating that a question was rollable. Players self-police it exactly as they do
 * at the table, and the app trusts them exactly as the table does.
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
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuestionBuilder {
  protected readonly store = inject(GameStore);

  protected readonly directions = DIRECTIONS;

  protected readonly terrains = TERRAINS;

  readonly ask = output<AskedQuestion>();

  /**
   * Ephemeral: it belongs to this half-built question and to nothing else.
   *
   * The plan puts the in-progress question in component state on purpose. The store holds what the
   * server said, and a half-chosen range is not something the server has said anything about.
   */
  protected readonly targetSeat = signal<number | null>(null);
  protected readonly start = signal<Direction>('N');
  protected readonly end = signal<Direction>('N');
  protected readonly terrain = signal<Terrain | null>(null);

  /** Everybody but the viewer. An eliminated player still answers, so they stay on the list. */
  protected readonly targets = computed(() => this.store.seats().filter((seat) => !seat.isViewer));

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

    this.ask.emit({ targetSeat: target, start: this.start(), end: this.end(), terrain: this.terrain() });
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
