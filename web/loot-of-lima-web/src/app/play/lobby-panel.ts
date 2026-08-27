import { ChangeDetectionStrategy, Component, computed, inject, output, resource, signal } from '@angular/core';
import { ApiClient } from '../core/api-client';
import { GameStore } from '../core/game-store';

/**
 * Choosing the deal and starting the game.
 *
 * Both live with the seated players rather than on the host screen, because the hub only accepts
 * `StartGame` from somebody sitting at the table: dealing the game and picking the scenario belong
 * to the people playing it, not to whoever happens to be holding the laptop.
 */
@Component({
  selector: 'app-lobby-panel',
  template: `
    <section class="card stack">
      <h3>Start the game</h3>

      @if (scenariosAllowed()) {
        <label class="field">
          <span>Deal</span>
          <select [value]="scenarioId() ?? ''" (change)="choose($any($event.target).value)">
            <option value="">Shuffle a new deal</option>
            @for (scenario of scenarios.value(); track scenario.id) {
              <option [value]="scenario.id">Book {{ scenario.book }}, scenario {{ scenario.number }}</option>
            }
          </select>
        </label>
        <p class="small muted">
          The printed scenarios are five-player puzzles, and their deals never pass tokens — their published answers
          describe the hands exactly as dealt.
        </p>
      } @else {
        <p class="small muted">
          A shuffled deal. The official scenarios are five-player only, so a {{ store.view()?.playerCount }}-player
          table always plays a generated one.
        </p>
      }

      <button type="button" class="button button--primary" [disabled]="!seated()" (click)="start.emit(scenarioId())">
        Deal and begin
      </button>
      @if (!seated()) {
        <p class="small muted">Take a seat first — only a player at the table can start the game.</p>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LobbyPanel {
  protected readonly store = inject(GameStore);

  private readonly api = inject(ApiClient);

  readonly start = output<string | null>();

  protected readonly scenarioId = signal<string | null>(null);

  protected readonly seated = computed(() => this.store.mySeat() !== null);

  protected readonly scenariosAllowed = computed(() => this.store.view()?.playerCount === 5);

  /**
   * Fetched once, and only where it can be used.
   *
   * All 239 of them, which is a long select and exactly the picker M5 is meant to replace. It is
   * here now because the first playable loop is a scenario game, and a text box for `b1s1` would
   * be a worse thing to have to throw away.
   */
  protected readonly scenarios = resource({
    params: () => (this.scenariosAllowed() ? {} : undefined),
    loader: () => this.api.scenarios(),
    defaultValue: [],
  });

  protected choose(value: string): void {
    this.scenarioId.set(value === '' ? null : value);
  }
}
