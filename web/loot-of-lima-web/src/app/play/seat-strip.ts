import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { GameStore } from '../core/game-store';
import { LocationToken } from './location-token';

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
  imports: [LocationToken, RouterLink],
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
            A spectator is offered the way in rather than a button that cannot work. Watching is a
            normal way to arrive here — the host screen links straight to it — and the hub refuses
            ClaimSeat from anybody who has not joined, with a message naming a step this page never
            offered. Joining happens at /join, behind the QR code.
          -->
          @if (store.phase() === 'Lobby') {
            @if (seat.isViewer) {
              <button type="button" class="button button--small" (click)="release.emit()">Leave seat</button>
            } @else if (seat.kind === 'Bot') {
              @if (canClaim()) {
                <button type="button" class="button button--small" (click)="claim.emit(seat.index)">Sit here</button>
              } @else if (store.roomCode(); as code) {
                <a class="button button--small" [routerLink]="['/join', code]">Join to sit here</a>
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
   * Whether this browser holds a player token for this table.
   *
   * Not the same question as holding a seat, which is what makes it worth passing in rather than
   * reading `isSpectator` off the store: somebody who has just come through the QR code has a
   * token and no seat, and taking one is exactly what they came to do. Somebody watching from the
   * host screen has neither, and the hub would refuse them.
   */
  readonly canClaim = input.required<boolean>();

  readonly claim = output<number>();

  readonly release = output<void>();
}
