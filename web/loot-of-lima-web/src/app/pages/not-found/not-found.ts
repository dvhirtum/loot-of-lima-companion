import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <main class="page page--narrow stack">
      <h1>Lost at sea</h1>
      <p class="muted">There is nothing at this address.</p>
      <a class="button" routerLink="/">Back to shore</a>
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotFound {}
