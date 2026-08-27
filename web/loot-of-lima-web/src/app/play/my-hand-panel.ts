import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { GameStore } from '../core/game-store';
import { LocationToken } from './location-token';

/**
 * The viewer's own tokens, and the face-up ones everybody has.
 *
 * Kept reachable in every phase rather than only while the tokens are being taken. The app owns
 * the deal and the physical tokens are on the table, so "which ones were mine again" is a question
 * a player asks in the middle of turn nine, not just at setup.
 */
@Component({
  selector: 'app-my-hand-panel',
  imports: [LocationToken],
  template: `
    <section class="card stack">
      <h3>Your tokens</h3>
      @if (store.myHand().length > 0) {
        <div class="row">
          @for (location of store.myHand(); track location) {
            <app-location-token [location]="location" />
          }
        </div>
      } @else if (store.isSpectator()) {
        <p class="muted small">You are watching this table without a seat.</p>
      } @else {
        <p class="muted small">Nothing dealt yet.</p>
      }

      @if (store.view(); as view) {
        @if (view.publicReveal.length > 0) {
          <h3>Face up</h3>
          <div class="row">
            @for (location of view.publicReveal; track location) {
              <app-location-token [location]="location" />
            }
          </div>
        }

        <!-- Null in every phase but Finished. This is the field the whole design exists to protect. -->
        @if (view.treasure; as treasure) {
          <h3>The treasure</h3>
          <div class="row">
            @for (location of treasure; track location) {
              <app-location-token [location]="location" />
            }
          </div>
        }
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyHandPanel {
  protected readonly store = inject(GameStore);
}
