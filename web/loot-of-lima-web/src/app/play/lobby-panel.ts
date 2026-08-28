import { ChangeDetectionStrategy, Component, computed, inject, output, resource, signal } from '@angular/core';
import { ApiClient } from '../core/api-client';
import { ScenarioSummary } from '../core/contracts';
import { GameStore } from '../core/game-store';

/**
 * Choosing the deal and starting the game.
 *
 * Both live with the seated players rather than on the host screen, because the hub only accepts
 * `StartGame` from somebody sitting at the table: dealing the game and picking the scenario belong
 * to the people playing it, not to whoever happens to be holding the laptop.
 *
 * The scenario is chosen as a book and a number, the two things printed on the booklet in the
 * player's hand. One select of all 239 was what M4 shipped to get a game started at all, and it is
 * unusable for its actual purpose: finding the scenario somebody just read out.
 */
@Component({
  selector: 'app-lobby-panel',
  template: `
    <section class="card stack">
      <h3>Start the game</h3>

      @if (scenariosAllowed()) {
        <div class="row" role="group" aria-label="Deal">
          <button type="button" class="button" [class.button--primary]="!fromBook()" (click)="fromBook.set(false)">
            Shuffle a new deal
          </button>
          <button type="button" class="button" [class.button--primary]="fromBook()" (click)="fromBook.set(true)">
            Play a printed scenario
          </button>
        </div>

        <!--
          Which option is chosen is marked on the options, not bound to the select.

          A value binding on the select is written during the update pass that *creates* this
          block, and the options are supplied by a control flow block whose views are created in
          that same pass — so the assignment can land on a select that is still empty, where
          setting a value no option carries does nothing. It is never re-applied either, because
          the signal it reads has not changed. The result is a select showing the first option
          while the component still holds the one the player chose, and the button below deals what
          the component holds. Marking the chosen option instead puts the write on the option
          itself, which cannot exist before there is something to mark.
        -->
        @if (fromBook() && loading()) {
          <p class="small muted">Fetching the list of printed scenarios…</p>
        } @else if (fromBook() && failed()) {
          <p class="small warn">The list of printed scenarios could not be loaded.</p>
          <button type="button" class="button" (click)="scenarios.reload()">Try again</button>
        } @else if (fromBook()) {
          <div class="row">
            <label class="field">
              <span>Book</span>
              <select (change)="chooseBook($any($event.target).value)">
                @for (option of books(); track option) {
                  <option [value]="option" [selected]="option === shownBook()">{{ option }}</option>
                }
              </select>
            </label>
            <label class="field">
              <span>Scenario</span>
              <select (change)="number.set(+$any($event.target).value)">
                @for (option of numbers(); track option) {
                  <option [value]="option" [selected]="option === shownNumber()">{{ option }}</option>
                }
              </select>
            </label>
          </div>

          <p class="small muted">
            The printed scenarios are five-player puzzles, and their deals never pass tokens — their published answers
            describe the hands exactly as dealt. Their setup announcements are the book's too, tie or no tie.
          </p>
        } @else {
          <p class="small muted">Twenty-four tokens shuffled fresh. Two go aside as the treasure, unseen by anybody.</p>
        }
      } @else {
        <p class="small muted">
          A shuffled deal. The official scenarios are five-player only, so a {{ store.view()?.playerCount }}-player
          table always plays a generated one.
        </p>
      }

      <!--
        Said here rather than above every question. A player expecting the app to check their roll
        would read its silence as approval, so it has to be said — but once, before the game starts,
        not as a standing line between the range and the button that sends it.
      -->
      <p class="small muted">
        The dice stay on the table. The app never asks what you rolled and never checks a question against one — that is
        the table's to police, exactly as it always was.
      </p>

      <button type="button" class="button button--primary" [disabled]="!canStart()" (click)="start.emit(chosen())">
        Deal and begin
      </button>
      @if (!seated()) {
        <p class="small muted">Take a seat first — only a player at the table can start the game.</p>
      } @else if (fromBook() && listed() && chosen() === null) {
        <!--
          One of the 240 is missing on purpose: book 16 prints scenario 12's tokens as a duplicate of
          scenario 11's, contradicting its own question table. It is an error in the book, so the
          scenario is absent rather than guessed at, and this is where a player meets that.

          Only once the list has actually arrived. The chosen pair is null while the fetch is in flight
          and null forever if it fails, so without that guard this blamed the printed book for the
          whole loading window and for every network fault — 238 pairs it is not about, and one it
          is.
        -->
        <p class="small muted">That scenario is not in the app — the printed book contradicts itself there.</p>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LobbyPanel {
  protected readonly store = inject(GameStore);

  private readonly api = inject(ApiClient);

  readonly start = output<string | null>();

  protected readonly fromBook = signal(false);
  protected readonly book = signal(1);
  protected readonly number = signal(1);

  protected readonly seated = computed(() => this.store.mySeat() !== null);

  protected readonly scenariosAllowed = computed(() => this.store.view()?.playerCount === 5);

  /**
   * Fetched once, and only where it can be used.
   *
   * The whole list, because the two selects are built from it: which books exist and which numbers
   * each book actually has are facts about the seed data, not a range this component may assume.
   */
  protected readonly scenarios = resource({
    params: () => (this.scenariosAllowed() ? {} : undefined),
    loader: () => this.api.scenarios(),
    defaultValue: [],
  });

  protected readonly loading = computed(() => this.scenarios.isLoading());

  protected readonly failed = computed(() => this.scenarios.error() !== undefined);

  /**
   * The scenarios there are, which is none until the fetch says otherwise.
   *
   * Every read below goes through here rather than through `scenarios.value()` directly, because a
   * resource in the error state re-throws the loader's error out of `value()` — it does not fall
   * back to `defaultValue`, which is easy to assume and wrong. An unguarded read therefore does not
   * leave the picker empty, it throws out of the render, and the panel that was going to explain
   * the failure never draws.
   */
  private readonly available = computed<readonly ScenarioSummary[]>(() =>
    this.failed() ? [] : this.scenarios.value(),
  );

  /**
   * Whether the list has actually arrived, as opposed to being empty because it has not.
   *
   * `defaultValue: []` means every question about the scenarios reads the same before the fetch
   * lands as it would if the seed data held nothing, so the panel needs this to tell "that pair is
   * not in the app" from "nobody has said yet".
   */
  protected readonly listed = computed(() => !this.loading() && !this.failed() && this.available().length > 0);

  protected readonly books = computed<readonly number[]>(() =>
    [...new Set(this.available().map((scenario) => scenario.book))].sort((left, right) => left - right),
  );

  /**
   * The book actually being shown, which is what was chosen only if the list holds it.
   *
   * `book` starts at 1 because it has to start somewhere, and nothing had reconciled that with the
   * books the fetch came back with. That is the same show-versus-hold split the options binding
   * had, arriving from the other side: with no option matching, the select falls back to its first
   * and paints a book the component is not holding, and the scenario list beside it comes up empty
   * because it is filtered on the one nobody can see.
   *
   * Resolved here rather than corrected by an effect, so there is no write during a render and no
   * moment where the two disagree.
   */
  protected readonly shownBook = computed(() => {
    const available = this.books();
    return available.includes(this.book()) ? this.book() : (available[0] ?? this.book());
  });

  protected readonly numbers = computed<readonly number[]>(() =>
    this.available()
      .filter((scenario) => scenario.book === this.shownBook())
      .map((scenario) => scenario.number)
      .sort((left, right) => left - right),
  );

  /** The same rule for the number: what was chosen, or the first this book has. */
  protected readonly shownNumber = computed(() => {
    const available = this.numbers();
    return available.includes(this.number()) ? this.number() : (available[0] ?? this.number());
  });

  /** The id the server knows this pair by, or null when the pair names no scenario the app holds. */
  protected readonly chosen = computed<string | null>(() => {
    if (!this.fromBook()) {
      return null;
    }

    // The resolved pair, not the raw signals: what is dealt has to be what the two selects are
    // showing, and those are drawn from these.
    const match = this.available().find(
      (scenario) => scenario.book === this.shownBook() && scenario.number === this.shownNumber(),
    );

    return match?.id ?? null;
  });

  protected readonly canStart = computed(() => this.seated() && (!this.fromBook() || this.chosen() !== null));

  /**
   * Moves to another book, keeping the number if that book has one.
   *
   * Every book has all twelve but one, so this almost always keeps the player where they were — and
   * in the case where it cannot, landing on the book's first scenario beats leaving a pair selected
   * that names nothing. That fallback used to be written out here, reading `numbers()` back
   * immediately after the write that changes it; `shownNumber` now expresses the same rule for
   * every way the pair can go stale, this one included.
   */
  protected chooseBook(value: string): void {
    this.book.set(Number(value));
  }
}
