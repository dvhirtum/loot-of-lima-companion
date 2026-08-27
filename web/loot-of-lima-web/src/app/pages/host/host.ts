import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiClient } from '../../core/api-client';
import { GameConnection } from '../../core/game-connection';
import { GameStore } from '../../core/game-store';

/**
 * The screen in the middle of the table, for as long as anybody is still arriving.
 *
 * It watches without a seat, so it is projected as a spectator and holds no hand: the big display
 * everybody can see is the last place a hand should ever be. What it shows is what is written on
 * the outside of the box — the code to scan and who has sat down.
 *
 * It has nothing to show once the deal happens, so it takes itself to the table. `ClaimSeat` is
 * refused outside `Lobby`, and joining is *not* — so a latecomer scanning the code after the deal
 * gets a player token and then a seat they cannot take, which is a worse dead end than the code
 * simply being gone. The laptop in the middle of the table should be showing the table by then
 * anyway.
 */
@Component({
  selector: 'app-host',
  imports: [RouterLink],
  template: `
    <main class="page stack">
      <header class="stack">
        <h1>{{ started() ? 'The game has begun' : 'Scan to join' }}</h1>
        <p class="roomcode">{{ roomCode() }}</p>
      </header>

      <div class="host">
        <!--
          Both the code and the way in disappear together, because after the deal they are the same
          dead end: scanning issues a token and then the hub refuses the seat. Everything below is
          what a player sees for the moment before the redirect lands, so it has to be true.
        -->
        @if (!started()) {
          <img class="qr" [src]="qr()" alt="QR code linking to this table" width="264" height="264" />
        }

        <section class="stack">
          <h2>At the table</h2>
          <ul class="seats">
            @for (seat of store.seats(); track seat.index) {
              <li [class.muted]="seat.kind === 'Bot'">
                {{ seat.displayName }}
                @if (seat.kind === 'Bot') {
                  <span class="small">— open</span>
                }
              </li>
            } @empty {
              <li class="muted">Waiting for the server…</li>
            }
          </ul>

          @if (started()) {
            <p class="banner banner--warn">Taking you to the table…</p>
          }

          <div class="row">
            @if (!started()) {
              <a class="button button--primary" [routerLink]="['/join', roomCode()]">Sit down here</a>
            }
            <!--
              Kept in both states. It is the manual form of the redirect, and the one thing on this
              screen that still works if navigation never happens.
            -->
            <a class="button" [routerLink]="['/play', roomCode()]">Watch the table</a>
          </div>
        </section>
      </div>
    </main>
  `,
  styles: `
    .host {
      display: flex;
      flex-wrap: wrap;
      gap: 2rem;
      align-items: flex-start;
    }

    .qr {
      background: var(--parchment);
      padding: 0.75rem;
      border-radius: var(--radius);
      image-rendering: pixelated;
    }

    .seats {
      list-style: none;
      margin: 0;
      padding: 0;
      font-size: 1.2rem;
      display: flex;
      flex-direction: column;
      gap: 0.3rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Host {
  /** Bound straight from the route by `withComponentInputBinding`. */
  readonly roomCode = input.required<string>();

  protected readonly store = inject(GameStore);

  private readonly api = inject(ApiClient);
  private readonly connection = inject(GameConnection);
  private readonly router = inject(Router);

  protected readonly qr = computed(() => this.api.qrCodeUrl(this.roomCode()));

  protected readonly started = computed(() => {
    const phase = this.store.phase();
    return phase !== null && phase !== 'Lobby';
  });

  constructor() {
    // No token: this screen is nobody, on purpose.
    effect(() => void this.connection.connect(this.roomCode(), null).catch(() => undefined));

    // Also the route a laptop takes when it is reloaded mid-game, since the phase arrives in the
    // first frame and this cannot tell that from a deal it just watched happen. That is the right
    // answer both times.
    //
    // `replaceUrl` matters more than it looks. Without it, Back from the table returns to a screen
    // whose only behaviour is to send you forward again, which is a trap rather than a history
    // entry. With it, Back goes wherever the laptop was before it opened the table.
    effect(() => {
      if (this.started()) {
        void this.router.navigate(['/play', this.roomCode()], { replaceUrl: true });
      }
    });

    inject(DestroyRef).onDestroy(() => {
      void this.connection.disconnect();
      this.store.reset();
    });
  }
}
