import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

/** The two things anybody ever wants here: open a table, or find one somebody else opened. */
@Component({
  selector: 'app-home',
  imports: [RouterLink],
  template: `
    <main class="page page--narrow stack">
      <header>
        <h1>Loot of Lima</h1>
        <p class="muted">
          A companion for the board game. The board, the screens and the tokens stay on the table — this replaces the
          scenario booklet and the bookkeeping.
        </p>
      </header>

      <section class="card stack">
        <h2>Open a table</h2>
        <a class="button button--primary" routerLink="/new">New game</a>
      </section>

      <section class="card stack">
        <h2>Join a table</h2>
        <p class="small muted">Scan the code on the host screen, or type it in.</p>
        <form class="row" (submit)="go($event)">
          <label class="field">
            <span>Room code</span>
            <input
              name="roomCode"
              autocapitalize="characters"
              autocomplete="off"
              maxlength="8"
              [value]="roomCode()"
              (input)="roomCode.set($any($event.target).value)"
            />
          </label>
          <button type="submit" class="button" [disabled]="roomCode().trim().length === 0">Join</button>
        </form>
      </section>
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  private readonly router = inject(Router);

  protected readonly roomCode = signal('');

  protected go(event: Event): void {
    event.preventDefault();

    const code = this.roomCode().trim().toUpperCase();
    if (code.length > 0) {
      void this.router.navigate(['/join', code]);
    }
  }
}
