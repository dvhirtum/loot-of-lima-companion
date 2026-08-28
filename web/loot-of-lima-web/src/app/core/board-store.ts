import { Injectable, computed, inject, resource } from '@angular/core';
import { ApiClient } from './api-client';
import { BoardLayoutDto } from './contracts';

/**
 * The island's geography, fetched once for the life of the app.
 *
 * The board is the same for every game ever played, so this is a constant that happens to live on
 * the server: `GET /api/board` serves the very file `BoardLayout.Load()` reads. It is asked for
 * rather than copied into this client because a copy would be a second source of truth about where
 * the locations are, and the whole reason the map is drawn from data is that there must not be one.
 *
 * Root-provided so the map and the accusation panel share one fetch rather than racing two.
 *
 * This is geography, not game state: it says B1 is in the north sector, which is printed on the
 * board in front of the player. Everything a game turns on still arrives through the hub.
 */
@Injectable({ providedIn: 'root' })
export class BoardStore {
  private readonly api = inject(ApiClient);

  private readonly board = resource({ loader: () => this.api.board() });

  /**
   * Whether the fetch failed, as opposed to not having finished.
   *
   * Told apart because they are not the same thing to look at. `layout` is null for both, so
   * without this the island sits on "Drawing the island…" forever and the accusation selects come
   * up empty — and the two panels this store feeds are the map and the only way to name a
   * treasure, so a fetch nobody mentions takes accusing with it and says nothing about why.
   *
   * There is no automatic retry. The board is fetched once for the life of the app, so a failure
   * here is a page that will not fix itself, and asking is what `reload` is for.
   */
  readonly failed = computed(() => this.board.error() !== undefined);

  readonly loading = computed(() => this.board.isLoading());

  /**
   * The layout, or null until there is one — including when there never will be.
   *
   * Asked of `failed` before the value is read, and that order is the whole of it: a resource in
   * the error state does not hand back its last value or a default, it *re-throws the loader's
   * error out of `value()`*. So an unguarded read does not degrade to an empty island, it throws
   * out of whatever is rendering — which for this store is the game page. `error()` may be asked
   * at any time; `value()` may not.
   */
  readonly layout = computed<BoardLayoutDto | null>(() => (this.failed() ? null : (this.board.value() ?? null)));

  /** All 24 location ids in sector order, which is the order the compass runs. */
  readonly locations = computed<readonly string[]>(() =>
    (this.layout()?.sectors ?? []).flatMap((sector) => sector.locations),
  );

  /** Asks again, for the button a player is offered when the first attempt failed. */
  reload(): void {
    this.board.reload();
  }
}
