import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiClient } from '../../core/api-client';

/**
 * How many seats, and nothing else.
 *
 * The deal is not chosen here. Every seat starts as a bot, so a table is playable the moment it
 * exists, and the choice of scenario belongs to whoever is sitting at it when they start —
 * which is a decision the hub enforces rather than one this page could make stick.
 */
@Component({
  selector: 'app-new-game',
  template: `
    <main class="page page--narrow stack">
      <h1>New game</h1>

      <section class="card stack">
        <h2>How many players?</h2>
        <p class="small muted">
          Every seat starts as a bot. Anyone who joins claims one; whatever is left stays a bot, and bots answer
          questions but never take turns.
        </p>
        <div class="row">
          @for (count of [3, 4, 5]; track count) {
            <button
              type="button"
              class="button"
              [class.button--primary]="playerCount() === count"
              (click)="playerCount.set(count)"
            >
              {{ count }}
            </button>
          }
        </div>
      </section>

      @if (problem(); as message) {
        <p class="banner banner--error" role="alert">{{ message }}</p>
      }

      <button type="button" class="button button--primary" [disabled]="busy()" (click)="create()">
        {{ busy() ? 'Opening…' : 'Open the table' }}
      </button>
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewGame {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);

  protected readonly playerCount = signal(5);
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);

  protected async create(): Promise<void> {
    this.busy.set(true);
    this.problem.set(null);

    try {
      const game = await this.api.createGame(this.playerCount());
      await this.router.navigate(['/host', game.roomCode]);
    } catch {
      this.problem.set('Could not open a table. Is the server running?');
    } finally {
      this.busy.set(false);
    }
  }
}
