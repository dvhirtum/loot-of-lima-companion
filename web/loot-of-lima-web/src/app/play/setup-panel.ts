import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { GameStore } from '../core/game-store';
import { TERRAINS, Terrain, TokenPassView } from '../core/contracts';
import { terrainOf } from '../core/locations';
import { LocationToken } from './location-token';

/** One terrain button, with how many of it the viewer holds and whether announcing it would be true. */
interface TerrainChoice {
  readonly terrain: Terrain;
  readonly held: number;
  readonly truthful: boolean;
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
            @if (forced()) {
              <p class="small muted">Say this aloud, then confirm. Holding none of a terrain makes it the fewest.</p>
            } @else {
              <p class="small muted">
                Say aloud which of these you hold fewest of. They are tied, so the choice is yours — and the table is
                not told there was a tie.
              </p>
            }
            <div class="row">
              @for (choice of choices(); track choice.terrain) {
                <!--
                  The untruthful ones are disabled rather than hidden. The count beside each is the
                  viewer's own hand, which they are looking at anyway, so showing all three is what
                  makes the offer checkable: a player can see *why* only one is open instead of
                  taking the app's word for it.
                -->
                <button
                  type="button"
                  class="button"
                  [disabled]="!choice.truthful"
                  [attr.aria-disabled]="!choice.truthful"
                  (click)="announce.emit(choice.terrain)"
                >
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
   * The three terrains with the viewer's own count beside each, and whether each is truthful.
   *
   * A count of a hand the player is looking at anyway, so it gives nothing away and makes the tie
   * clause real: seeing two zeroes is what turns "announce your fewest" into the free choice the
   * rulebook says it is. Nothing about the count leaves this browser, and no other client is told
   * how many terrains were eligible.
   *
   * `truthful` is the one place this client does arithmetic the engine also does, and it is a
   * deliberate, bounded exception to "the client holds no rules logic". It is derived from the
   * viewer's own complete hand and from nothing else, so it cannot reach anything this client was
   * not told; it decides only whether a button is pressable; and `FewestTerrainRule.IsTruthful`
   * stays the authority, refusing an untruthful announcement however it arrives. Offering a button
   * whose only outcome is a refusal is the same defect as the seat a spectator could not take.
   */
  protected readonly choices = computed<readonly TerrainChoice[]>(() => {
    const hand = this.store.myHand();
    const held = TERRAINS.map((terrain) => ({
      terrain,
      held: hand.filter((location) => terrainOf(location) === terrain).length,
    }));

    const fewest = Math.min(...held.map((choice) => choice.held));
    return held.map((choice) => ({ ...choice, truthful: choice.held === fewest }));
  });

  /**
   * Whether the rule leaves this seat no choice at all.
   *
   * Only ever read to change the wording, never to skip the click. Announcing automatically when
   * there is one legal answer would tell the whole table which seats were tied: every other seat
   * resolves the instant the phase reaches it, so a seat that stops and waits for a human is a seat
   * that had a tie. That is precisely what the rulebook forbids revealing, and it would be the
   * plainest possible signal -- visible on every screen, in the "waiting for ..." line.
   */
  protected readonly forced = computed(() => this.choices().filter((choice) => choice.truthful).length === 1);

  protected nameOf(seatIndex: number | null): string {
    if (seatIndex === null) {
      return 'nobody';
    }

    return this.store.seats().find((seat) => seat.index === seatIndex)?.displayName ?? `Seat ${seatIndex + 1}`;
  }
}
