import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Direction, Terrain } from '../core/contracts';
import { GameStore } from '../core/game-store';
import { LocationToken } from './location-token';

/**
 * Everything that has happened, as this viewer is allowed to see it.
 *
 * There is one branch per history type and no branch reads a field off the wrong one, because the
 * server does not send one shape with holes in it: a viewer who was not party to a secret question
 * receives `secretQuestionOccurred`, which has no property for the range or the answer at all. The
 * same goes for a token pass and for a failed accusation. Rendering redacted entries as their own
 * cases, rather than as the full case with missing values, is this client keeping its half of that
 * bargain -- there is nowhere here for a `??` to quietly invent something.
 *
 * This is also as far as the app goes towards solving. It shows what was asked and answered; the
 * deducing is the player's.
 */
@Component({
  selector: 'app-history-panel',
  imports: [LocationToken],
  templateUrl: './history-panel.html',
  styles: `
    .log {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.45rem;
      max-height: 26rem;
      overflow-y: auto;
    }

    .entry {
      display: grid;
      grid-template-columns: 2.2rem minmax(0, 1fr) auto;
      gap: 0.5rem;
      align-items: baseline;
      padding: 0.4rem 0.55rem;
      border-radius: 6px;
      background: var(--raised);
      border-left: 2px solid var(--line);
    }

    /* Tokens sit inline with the words around them, so the sentence still reads as a sentence. */
    .body app-location-token {
      display: inline-block;
      vertical-align: baseline;
      margin: 0 0.1rem;
    }

    .entry--secret {
      border-left-color: var(--brass);
    }

    .entry--quiet {
      border-left-color: transparent;
      color: var(--parchment-dim);
    }

    .entry--end {
      border-left-color: var(--coral);
    }

    .turn {
      font-family: var(--font-mono);
      font-size: 0.75rem;
      color: var(--parchment-dim);
    }

    .answer {
      font-family: var(--font-mono);
      font-weight: 700;
      color: var(--brass-bright);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistoryPanel {
  protected readonly store = inject(GameStore);

  protected nameOf(seatIndex: number): string {
    return this.store.seats().find((seat) => seat.index === seatIndex)?.displayName ?? `Seat ${seatIndex + 1}`;
  }

  /**
   * The question in words, using the same half-open reading the question builder shows.
   *
   * Written out rather than abbreviated to `N→SE` because the arc is the part of this game people
   * get wrong, and a log that reads back the way the question was asked is what makes an answer
   * checkable against the tokens in front of you.
   */
  protected phrase(start: Direction, end: Direction, terrain: Terrain | null): string {
    const scope = terrain === null ? 'locations' : `${terrain.toLowerCase()} locations`;
    const arc = start === end ? 'anywhere' : `from ${start} up to ${end}`;
    return `${scope} ${arc}`;
  }
}
