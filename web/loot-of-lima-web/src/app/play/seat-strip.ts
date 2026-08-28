import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { GameStore } from '../core/game-store';
import { LocationToken } from './location-token';

/**
 * One seat being taken.
 *
 * `name` is null when this browser has already joined this table, which is every claim after the
 * first: the identity it holds carries the name it gave, and asking again would let one person be
 * two people at one table.
 */
export interface SeatClaim {
  readonly seatIndex: number;
  readonly name: string | null;
}

/**
 * The table, one card per seat.
 *
 * Every number on a seat card is public by the rules: hand sizes never change, ability tokens sit
 * face up on the player board, and the setup announcement is made aloud. `knownHand` is the one
 * exception, and it is only ever populated for seats the viewer legitimately learned something
 * about -- which at this milestone means their own.
 */
@Component({
  selector: 'app-seat-strip',
  imports: [LocationToken],
  template: `
    <ul class="seats">
      @for (seat of store.seats(); track seat.index) {
        <li class="seat" [class.seat--active]="seat.isActive" [class.seat--out]="seat.eliminated">
          <div class="seat__head">
            <strong>{{ seat.displayName }}</strong>
            @if (seat.isViewer) {
              <span class="tag tag--you">you</span>
            } @else if (seat.kind === 'Bot') {
              <span class="tag">bot</span>
            }
            @if (seat.isActive) {
              <span class="tag tag--turn">to act</span>
            }
            @if (seat.eliminated) {
              <span class="tag tag--out">eliminated</span>
            }
          </div>

          <!--
            Nothing but the name until the deal happens. In the lobby every seat truthfully holds
            no tokens and every ability, and printing "0 tokens · Barrel, Shovel, Pistol" five
            times says nothing while looking like it does.
          -->
          @if (store.phase() !== 'Lobby') {
            <p class="small muted seat__facts">
              {{ seat.handSize }} tokens
              @if (seat.fewestTerrain; as terrain) {
                · fewest {{ terrain.toLowerCase() }}
              }
              @if (seat.abilitiesRemaining.length > 0) {
                · {{ seat.abilitiesRemaining.join(', ') }}
              } @else {
                · no abilities left
              }
            </p>
          }

          <!--
            Shown for any seat that has any, not just the viewer's own. A token learned by passing
            is knowledge about somebody else's hand, and hiding it would throw away the only thing
            the passing phase exists to give you.
          -->
          @if (seat.knownHand.length > 0) {
            <div class="row seat__known">
              @for (location of seat.knownHand; track location) {
                <app-location-token [location]="location" />
              }
              @if (!seat.handIsComplete) {
                <span class="small muted">of {{ seat.handSize }}</span>
              }
            </div>
          }

          <!--
            Taking a seat, and the only place this app asks anybody who they are.

            It used to be a page in front of the table: the QR code opened a form, the form asked
            for a name, and only then did the game appear. Everybody who scanned the code paid for
            a seat before they had seen whether they wanted one, and a watcher who had skipped the
            form was offered a link back to it beside every open seat. Now the code opens the table
            and the name is asked for here, at the moment it is first needed and beside the seat it
            is being given to.

            The field replaces the button in the one card being claimed rather than opening over the
            table, for the same reason the accusation is a panel and not a modal: the seats are what
            is being chosen between, and covering them to ask about one of them is backwards.

            A seat is offered only to somebody who has not got one. ClaimSeat refuses a second
            outright — one identity, one hand, or the projector would build a full view of two hands
            for one person — so the other cards a seated player used to be offered were buttons that
            could only ever answer PlayerAlreadySeated. Moving is leaving and sitting down again,
            and the button for the first half is on your own card.

            No backticks anywhere in here: this template is a template literal, so one inside a
            comment ends the string and the component stops parsing.
          -->
          @if (store.phase() === 'Lobby') {
            @if (seat.isViewer) {
              <button type="button" class="button button--small" (click)="release.emit()">Leave seat</button>
            } @else if (seat.kind === 'Bot' && store.mySeat() === null) {
              @if (naming() === seat.index) {
                <form class="stack seat__naming" (submit)="sitDown($event, seat.index)">
                  <label class="field">
                    <span>Your name</span>
                    <input
                      name="displayName"
                      autocomplete="nickname"
                      maxlength="24"
                      [value]="name()"
                      (input)="name.set($any($event.target).value)"
                    />
                  </label>
                  <div class="row">
                    <button
                      type="submit"
                      class="button button--small button--primary"
                      [disabled]="name().trim().length === 0"
                    >
                      Sit down
                    </button>
                    <button type="button" class="button button--small" (click)="naming.set(null)">Cancel</button>
                  </div>
                </form>
              } @else {
                <button type="button" class="button button--small" (click)="sitHere(seat.index)">Sit here</button>
              }
            }
          }
        </li>
      }
    </ul>
  `,
  styles: `
    .seats {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.75rem;
      grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
    }

    .seat {
      background: var(--raised);
      border: 1px solid var(--line);
      border-left: 3px solid transparent;
      border-radius: var(--radius);
      padding: 0.7rem 0.85rem;
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
      align-items: flex-start;
    }

    .seat--active {
      border-left-color: var(--brass);
    }

    .seat--out {
      opacity: 0.55;
    }

    .seat__head {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
      align-items: baseline;
    }

    .seat__facts {
      margin: 0;
    }

    .seat__known {
      gap: 0.3rem;
    }

    /* Full width of the card, so the field does not sit in a column of its own beside nothing. */
    .seat__naming {
      align-self: stretch;
      gap: 0.5rem;
    }

    .tag {
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      padding: 0.1rem 0.4rem;
      border-radius: 4px;
      border: 1px solid var(--line);
      color: var(--parchment-dim);
    }

    .tag--you {
      border-color: var(--brass);
      color: var(--brass-bright);
    }

    .tag--turn {
      background: var(--brass);
      border-color: var(--brass);
      color: var(--ink);
    }

    .tag--out {
      border-color: var(--coral);
      color: var(--coral);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeatStrip {
  protected readonly store = inject(GameStore);

  /**
   * Whether this browser has to say who it is before it can take a seat.
   *
   * Passed in rather than read off the store, because it is not a question about the game: it asks
   * whether this browser is holding a player token for this table, which the store has no idea
   * about. False for anybody who has already joined -- they have a name, and every seat they take
   * afterwards is taken under it.
   */
  readonly needsName = input.required<boolean>();

  readonly claim = output<SeatClaim>();

  readonly release = output<void>();

  /**
   * Which seat's name field is open, if any.
   *
   * Local rather than in `TableUi`, which is for the state two components share. Nothing outside
   * this strip has any use for a half-typed name, and it is gone the moment the seat is taken.
   */
  protected readonly naming = signal<number | null>(null);

  protected readonly name = signal('');

  /** Claims outright when this browser already has a name, and asks for one when it does not. */
  protected sitHere(seatIndex: number): void {
    if (!this.needsName()) {
      this.claim.emit({ seatIndex, name: null });
      return;
    }

    // Cleared on the way in as well as on the way out. Opening one seat's field on a name typed
    // for a different seat and abandoned is how somebody sits down as the wrong person.
    this.name.set('');
    this.naming.set(seatIndex);
  }

  protected sitDown(event: Event, seatIndex: number): void {
    event.preventDefault();

    const name = this.name().trim();
    if (name.length === 0) {
      return;
    }

    this.naming.set(null);
    this.claim.emit({ seatIndex, name });
  }
}
