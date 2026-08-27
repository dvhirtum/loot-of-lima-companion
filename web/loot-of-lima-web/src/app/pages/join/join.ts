import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiClient } from '../../core/api-client';
import { PlayerIdentityStore } from '../../core/player-identity';

/**
 * Where a scanned QR code lands.
 *
 * Joining gets an identity and nothing more. Which seat is the player's own choice, made over the
 * hub on the next page where the whole table can watch the lobby fill up — and where the server
 * can refuse a seat somebody else has already taken.
 */
@Component({
  selector: 'app-join',
  template: `
    <main class="page page--narrow stack">
      <h1>Join {{ roomCode() }}</h1>

      <form class="card stack" (submit)="join($event)">
        <label class="field">
          <span>Your name</span>
          <input
            name="displayName"
            autocomplete="nickname"
            maxlength="24"
            [value]="displayName()"
            (input)="displayName.set($any($event.target).value)"
          />
        </label>

        @if (problem(); as message) {
          <p class="banner banner--error" role="alert">{{ message }}</p>
        }

        <button type="submit" class="button button--primary" [disabled]="busy() || displayName().trim().length === 0">
          {{ busy() ? 'Joining…' : 'Join the table' }}
        </button>
      </form>

      <p class="small muted">
        Your seat is held by a token kept in this browser, so refreshing the page or locking the phone puts you back
        where you were.
      </p>
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Join {
  readonly roomCode = input.required<string>();

  private readonly api = inject(ApiClient);
  private readonly identities = inject(PlayerIdentityStore);
  private readonly router = inject(Router);

  protected readonly displayName = signal('');
  protected readonly busy = signal(false);
  protected readonly problem = signal<string | null>(null);

  protected async join(event: Event): Promise<void> {
    event.preventDefault();

    const name = this.displayName().trim();
    if (name.length === 0) {
      return;
    }

    this.busy.set(true);
    this.problem.set(null);

    try {
      const joined = await this.api.join(this.roomCode(), name);
      this.identities.remember(this.roomCode(), joined, name);
      await this.router.navigate(['/play', this.roomCode()]);
    } catch {
      this.problem.set(`No live table has the code ${this.roomCode()}.`);
    } finally {
      this.busy.set(false);
    }
  }
}
