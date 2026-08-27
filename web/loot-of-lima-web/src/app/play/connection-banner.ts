import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { GameStore } from '../core/game-store';

/**
 * Whether this client is still hearing from the table, and what the server last refused.
 *
 * Both belong together because both answer the same question a player actually asks: is what I am
 * looking at current? A silent disconnect is the worst failure this app has -- the board simply
 * stops changing and looks fine doing it.
 */
@Component({
  selector: 'app-connection-banner',
  template: `
    @if (warning(); as text) {
      <p class="banner banner--warn" role="status">{{ text }}</p>
    }
    @if (store.error(); as error) {
      <p class="banner banner--error" role="alert">
        {{ error.message }}
        <button type="button" class="button button--small" (click)="store.clearError()">Dismiss</button>
      </p>
    }
  `,
  styles: `
    :host {
      display: contents;
    }

    .banner {
      display: flex;
      gap: 0.75rem;
      align-items: center;
      justify-content: space-between;
      margin: 0;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConnectionBanner {
  protected readonly store = inject(GameStore);

  /** Nothing at all while connected, because a banner that is always there is not a banner. */
  protected readonly warning = computed(() => {
    switch (this.store.status()) {
      case 'connecting':
        return 'Connecting to the table…';
      case 'reconnecting':
        return 'Lost the table. Reconnecting…';
      case 'disconnected':
        return 'Disconnected. Reload the page to rejoin — your seat is held by this browser.';
      default:
        return null;
    }
  });
}
