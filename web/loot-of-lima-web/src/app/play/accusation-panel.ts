import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { BoardStore } from '../core/board-store';
import { GameStore } from '../core/game-store';
import { locationLabel } from '../core/locations';
import { LocationToken } from './location-token';
import { TableUi } from './table-ui';

/** An accusation, once both halves have been named. */
export interface NamedTreasure {
  readonly first: string;
  readonly second: string;
}

/**
 * Naming the two treasure locations.
 *
 * A panel rather than a modal dialog, and that is a decision rather than a shortcut: the two
 * locations are chosen by tapping the island, and a modal would cover the very thing being tapped.
 * It stays folded shut behind one button so nobody opens it by brushing past — an accusation cannot
 * be taken back, and a wrong one puts you out of the game for the rest of it.
 *
 * Offered whatever the turn is. Accusation is legal at any moment, including somebody else's turn,
 * which is a rule that would be invisible if the only route to it were the turn panel.
 *
 * A table of one is the exception the copy has to carry, because there the rule genuinely differs:
 * nothing eliminates the only player, so a wrong pair is a ruled-out pair and the game goes on.
 * Giving up is what ends it instead, and that button appears only once there is something to give
 * up on — the server refuses it before then, so this is the shape of the rule rather than a
 * courtesy laid over it.
 */
@Component({
  selector: 'app-accusation-panel',
  imports: [LocationToken],
  template: `
    <section class="card stack">
      @if (!ui.accusing()) {
        <h3>Accusation</h3>
        @if (store.isSolo()) {
          <p class="small muted">
            Name both treasure locations. Right and you have solved it. Wrong and you are told so and carry on — there
            is nobody here to be put out of the game in front of, and ending it would only mean being handed the answer
            for having guessed badly.
          </p>
        } @else {
          <p class="small muted">
            Name both treasure locations, on your turn or anybody else's. Right and you win. Wrong and you are out for
            the rest of the game — you still answer questions, and nothing you named is revealed.
          </p>
        }
        <button type="button" class="button" [disabled]="!canOpen()" (click)="ui.setAccusing(true)">Accuse</button>
        @if (eliminated()) {
          <p class="small muted">You are eliminated. Answering is all that is left to you.</p>
        }

        @if (canGiveUp()) {
          <div class="stack surrender">
            <p class="small muted">
              {{ wrong() }} wrong so far. Each one rules a pair out, which is a real deduction and yours — but if you
              would rather stop and see it:
            </p>
            <button type="button" class="button" (click)="giveUp.emit()">Give up and reveal the treasure</button>
            <p class="small warn">That ends the game.</p>
          </div>
        }
      } @else {
        <h3>Name the treasure</h3>
        <p class="small muted">Tap two locations on the island, or choose them here.</p>

        <div class="pair">
          @for (slot of slots; track slot) {
            <label class="field">
              <span>{{ slot === 0 ? 'First' : 'Second' }}</span>
              <select [value]="ui.picks()[slot] ?? ''" (change)="choose(slot, $any($event.target).value)">
                <option value="">Choose…</option>
                <!-- The value is the server's id and the caption is the box's, so what travels in
                     the accusation is still B1 while what a player picks from reads 1B. -->
                @for (location of board.locations(); track location) {
                  <option [value]="location">{{ label(location) }}</option>
                }
              </select>
            </label>
          }
        </div>

        @if (ui.named().length > 0) {
          <div class="row">
            @for (location of ui.named(); track location) {
              <app-location-token [location]="location" />
            }
          </div>
        }

        <div class="row">
          <button type="button" class="button button--primary" [disabled]="!ui.canAccuse()" (click)="confirm()">
            Accuse
          </button>
          <button type="button" class="button" (click)="ui.setAccusing(false)">Cancel</button>
        </div>
        <p class="small warn">There is no taking this back.</p>
      }
    </section>
  `,
  styles: `
    .pair {
      display: grid;
      gap: 0.5rem;
      grid-template-columns: repeat(auto-fit, minmax(8rem, 1fr));
    }

    .warn {
      margin: 0;
      color: var(--coral);
    }

    /* Ruled off, because it is the opposite of the button above it and sits in the same card. */
    .surrender {
      border-top: 1px solid var(--line);
      padding-top: 0.75rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccusationPanel {
  protected readonly store = inject(GameStore);
  protected readonly ui = inject(TableUi);
  protected readonly board = inject(BoardStore);

  readonly accuse = output<NamedTreasure>();

  readonly giveUp = output<void>();

  protected readonly slots = [0, 1] as const;

  /** How a location is written on the box: the number first. The value stays the server's id. */
  protected readonly label = locationLabel;

  protected readonly eliminated = computed(() => this.store.mySeat()?.eliminated === true);

  /** Seated, playing, and not already out. A spectator has nothing to lose and no seat to lose it from. */
  protected readonly canOpen = computed(
    () => this.store.phase() === 'InProgress' && this.store.mySeat() !== null && !this.eliminated(),
  );

  protected readonly wrong = computed(() => this.store.myWrongAccusations());

  /**
   * Whether there is anything to give up on yet.
   *
   * The same three conditions the server checks, in the same order, because a button that is only
   * refused when pressed is the defect this app has already made once. The server is still the one
   * that decides — this only decides whether to draw it.
   */
  protected readonly canGiveUp = computed(
    () => this.store.phase() === 'InProgress' && this.store.isSolo() && this.wrong() > 0,
  );

  protected choose(slot: 0 | 1, value: string): void {
    this.ui.setPick(slot, value === '' ? null : value);
  }

  protected confirm(): void {
    const [first, second] = this.ui.picks();
    if (first === null || second === null) {
      return;
    }

    this.accuse.emit({ first, second });
    this.ui.setAccusing(false);
  }
}
