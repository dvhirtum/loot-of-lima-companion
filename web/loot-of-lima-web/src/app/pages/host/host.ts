import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../../core/api-client';
import { GameConnection } from '../../core/game-connection';
import { GameStore } from '../../core/game-store';

/**
 * The screen in the middle of the table.
 *
 * It watches without a seat, so it is projected as a spectator and holds no hand: the big display
 * everybody can see is the last place a hand should ever be. What it shows is what is written on
 * the outside of the box — the code to scan and who has sat down.
 */
@Component({
  selector: 'app-host',
  imports: [RouterLink],
  template: `
    <main class="page stack">
      <header class="stack">
        <h1>Scan to join</h1>
        <p class="roomcode">{{ roomCode() }}</p>
      </header>

      <div class="host">
        <img class="qr" [src]="qr()" alt="QR code linking to this table" width="264" height="264" />

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
            <p class="banner banner--warn">The game has begun. This screen keeps watching.</p>
          }

          <div class="row">
            <a class="button button--primary" [routerLink]="['/join', roomCode()]">Sit down here</a>
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

  protected readonly qr = computed(() => this.api.qrCodeUrl(this.roomCode()));

  protected readonly started = computed(() => {
    const phase = this.store.phase();
    return phase !== null && phase !== 'Lobby';
  });

  constructor() {
    // No token: this screen is nobody, on purpose.
    effect(() => void this.connection.connect(this.roomCode(), null).catch(() => undefined));

    inject(DestroyRef).onDestroy(() => {
      void this.connection.disconnect();
      this.store.reset();
    });
  }
}
