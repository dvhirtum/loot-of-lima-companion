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

  readonly layout = computed<BoardLayoutDto | null>(() => this.board.value() ?? null);

  /** All 24 location ids in sector order, which is the order the compass runs. */
  readonly locations = computed<readonly string[]>(() =>
    (this.board.value()?.sectors ?? []).flatMap((sector) => sector.locations),
  );
}
