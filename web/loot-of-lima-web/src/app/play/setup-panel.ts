import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { GameStore } from '../core/game-store';
import { TERRAINS, Terrain, TokenPassView } from '../core/contracts';
import { terrainOf } from '../core/locations';
import { LocationToken } from './location-token';

/** One terrain button, with how many of it the viewer holds. */
interface TerrainChoice {
  readonly terrain: Terrain;
  readonly held: number;
}

/**
 * The three gates between the deal and the first question: take your tokens, pass one, announce
 * the terrain you hold fewest of.
 *
 * They share a panel because they share a shape -- each is one thing this seat owes the table
 * before play can start -- and because only one of them is ever on screen at a time.
 */
@Component({
  selector: 'app-setup-panel',
  imports: [LocationToken],
  template: `
    <section class="card stack">
      @switch (store.phase()) {
        @case ('Dealing') {
          <h3>Take your tokens</h3>
          <p class="small muted">
            Find these on the table and put them behind your screen. The app dealt them, so nobody has to type a hand
            in.
          </p>
          <button type="button" class="button button--primary" [disabled]="waiting()" (click)="confirmTokens.emit()">
            {{ waiting() ? 'Waiting for the others…' : 'I have them' }}
          </button>
        }

        @case ('Passing') {
          <h3>Pass a token</h3>
          @for (pass of myPasses(); track pass.sequence) {
            @if (pass.giverSeat === mySeatIndex()) {
              <p class="row">
                <span>Hand</span>
                <app-location-token [location]="pass.location" />
                <span>to {{ nameOf(pass.receiverSeat) }} ({{ pass.direction.toLowerCase() }}).</span>
              </p>
            } @else {
              <p class="row">
                <span>{{ nameOf(pass.giverSeat) }} passes you</span>
                <app-location-token [location]="pass.location" />
              </p>
            }
          }
          <button type="button" class="button button--primary" [disabled]="waiting()" (click)="confirmPass.emit()">
            {{ waiting() ? 'Waiting for the others…' : 'Done passing' }}
          </button>
        }

        @case ('Announcing') {
          <h3>Announce your fewest terrain</h3>
          @if (myTurn()) {
            <p class="small muted">
              Say aloud which terrain you hold fewest of. Holding none of one makes it the fewest, and if two are tied
              the choice is yours — the table is not told there was a tie.
            </p>
            <div class="row">
              @for (choice of choices(); track choice.terrain) {
                <button type="button" class="button" (click)="announce.emit(choice.terrain)">
                  {{ choice.terrain }} <span class="muted small">({{ choice.held }})</span>
                </button>
              }
            </div>
          } @else {
            <p class="muted small">Waiting for {{ nameOf(store.activeSeat()) }} to announce.</p>
          }
        }
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SetupPanel {
  protected readonly store = inject(GameStore);

  readonly confirmTokens = output<void>();

  readonly confirmPass = output<void>();

  readonly announce = output<Terrain>();

  protected readonly mySeatIndex = computed(() => this.store.mySeat()?.index ?? null);

  protected readonly myTurn = computed(() => this.store.activeSeat() === this.mySeatIndex());

  /** Whether this seat has already done what the phase asks of it and is waiting on the others. */
  protected readonly waiting = computed(() => {
    const seat = this.store.mySeat();
    if (seat === null) {
      return true;
    }

    return this.store.phase() === 'Passing' ? seat.passConfirmed : seat.tokensConfirmed;
  });

  /** Both directions of the viewer's own passes. The other seats' passes are not in this view at all. */
  protected readonly myPasses = computed<readonly TokenPassView[]>(() =>
    this.store
      .history()
      .filter((entry): entry is TokenPassView => entry.type === 'tokenPass')
      // Back into the order they happened in: the store hands history out newest first, which is
      // right for a log and wrong for an instruction telling somebody what to do next.
      .slice()
      .sort((left, right) => left.sequence - right.sequence),
  );

  /**
   * The three terrains with the viewer's own count beside each.
   *
   * A count of a hand the player is looking at anyway, so it gives nothing away and makes the tie
   * clause real: seeing two zeroes is what turns "announce your fewest" into the free choice the
   * rulebook says it is. Nothing about the count leaves this browser, and no other client is told
   * how many terrains were eligible.
   */
  protected readonly choices = computed<readonly TerrainChoice[]>(() => {
    const hand = this.store.myHand();
    return TERRAINS.map((terrain) => ({
      terrain,
      held: hand.filter((location) => terrainOf(location) === terrain).length,
    }));
  });

  protected nameOf(seatIndex: number | null): string {
    if (seatIndex === null) {
      return 'nobody';
    }

    return this.store.seats().find((seat) => seat.index === seatIndex)?.displayName ?? `Seat ${seatIndex + 1}`;
  }
}
