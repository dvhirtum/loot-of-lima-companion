import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { BoardStore } from '../core/board-store';
import { BoardLayoutDto, Direction, TERRAINS, Terrain } from '../core/contracts';
import { locationLabel, terrainOf } from '../core/locations';
import {
  BAND_RADIUS,
  Band,
  COMPASS_RADIUS,
  ISLAND_INNER_RADIUS,
  ISLAND_OUTER_RADIUS,
  SECTOR_COUNT,
  TOKEN_SIZE,
  VIEW_BOX,
  arcPath,
  boundaryAngle,
  pointAt,
  sectorInRange,
  tokenPoint,
} from './map-geometry';
import { TableUi } from './table-ui';

/** One of the three terrain bands, placed and named. */
interface MapBand extends Band {
  readonly terrain: Terrain;
}

/** One band's highlighted segment, or the whole band's ring when the arrows coincide. */
interface MapArc {
  readonly terrain: Terrain;
  readonly d: string;
}

/** One of the 24 tokens, placed and coloured. */
interface MapToken {
  /** The server's id, `B1`. Everything that identifies a token uses this: `data-location`, a tap. */
  readonly location: string;
  /** The same token as the box writes it, `1B`. Everything a player reads uses this. */
  readonly label: string;
  readonly terrain: Terrain;
  readonly x: number;
  readonly y: number;
  /** Inside the range being built, which is a question on screen and not a fact about the game. */
  readonly lit: boolean;
  readonly picked: boolean;
}

interface Divider {
  readonly x1: number;
  readonly y1: number;
  readonly x2: number;
  readonly y2: number;
}

interface CompassLabel {
  readonly direction: Direction;
  readonly x: number;
  readonly y: number;
}

/**
 * The island, drawn rather than photographed.
 *
 * Inline SVG built from the board layout the server serves at `GET /api/board` -- the same JSON
 * `BoardLayout.Load()` reads -- so the map and the rules cannot drift apart. An image could not
 * make that promise, would not be crisp on a phone held at arm's length across a table, and could
 * not be tapped to name a location.
 *
 * **The map is about terrain, and about nothing else.** Three coloured bands, 24 coloured tokens,
 * and a highlight showing which band and which slice the question being built covers. It does not
 * say whose a token is, and it cannot: this component injects `BoardStore` and `TableUi` and
 * emphatically not `GameStore`, so no hand, no reveal, no pass and no treasure is reachable from
 * here at all. The leak the old map had to be tested against is now a compile error rather than a
 * spec -- there is no expression in this class that could name a token's owner.
 *
 * What survives of the viewer is the range they are currently building, which they typed
 * themselves, and the two locations they have tapped for an accusation. Both come from `TableUi`,
 * which knows nothing about a game either.
 */
@Component({
  selector: 'app-island-map',
  templateUrl: './island-map.html',
  styleUrl: './island-map.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IslandMap {
  protected readonly ui = inject(TableUi);

  private readonly board = inject(BoardStore);

  protected readonly viewBox = VIEW_BOX;
  protected readonly tokenSize = TOKEN_SIZE;

  protected readonly ready = computed(() => this.board.layout() !== null);

  /**
   * The three bands, shore outside forest outside peaks.
   *
   * Drawn as whole rings and always the same three, because that is what the island is. They are
   * geography exactly as the compass letters are, and the printed board is painted the same way.
   */
  protected readonly bands = computed<readonly MapBand[]>(() =>
    TERRAINS.map((terrain) => ({ terrain, ...BAND_RADIUS[terrain] })),
  );

  /**
   * The eight compass lines, drawn because the arrows point at them.
   *
   * This is the part of the board that is easiest to get wrong from the rules alone: a range runs
   * from line to line, and the tokens sit in the gaps between the lines rather than on them. Drawing
   * the lines makes "start included, end excluded" something a player can see instead of something
   * they have to be told.
   */
  protected readonly boundaries = computed<readonly Divider[]>(() =>
    Array.from({ length: SECTOR_COUNT }, (_unused, index) => {
      const angle = boundaryAngle(index);
      const inner = pointAt(angle, ISLAND_INNER_RADIUS);
      const outer = pointAt(angle, ISLAND_OUTER_RADIUS);
      return { x1: inner.x, y1: inner.y, x2: outer.x, y2: outer.y };
    }),
  );

  /**
   * The eight letters, each at the far end of its own compass line.
   *
   * On the line and not in the middle of a sector, because that is what the dial's arrows point at
   * and what the two selects above the map name. A letter floating in the middle of a slice would
   * invite a player to read "from NE" as the middle of the north-east gap, and the wedge would then
   * appear to start somewhere the question did not say. Sector k is the gap *clockwise* from line k,
   * which is why B1, F1 and M1 sit between the N letter and the NE one.
   */
  protected readonly compass = computed<readonly CompassLabel[]>(() =>
    (this.board.layout()?.sectors ?? []).map((sector, index) => {
      const point = pointAt(boundaryAngle(index), COMPASS_RADIUS);
      return { direction: sector.direction, x: point.x, y: point.y };
    }),
  );

  /**
   * The lit segment of each band the question being built covers: one band for a terrain question,
   * all three for a question about anywhere.
   *
   * A preview and only a preview: it says which slice of which terrain the question is about, never
   * how many tokens are in it. The answer arrives from the server like every other answer.
   */
  protected readonly arcs = computed<readonly MapArc[]>(() => {
    const range = this.range();

    if (range === null) {
      return [];
    }

    return range.terrains.flatMap<MapArc>((terrain) => {
      const band = BAND_RADIUS[terrain];
      const d = arcPath(range.start, range.end, range.fullCircleWhenEqual, band.inner, band.outer);

      return d === '' ? [] : [{ terrain, d }];
    });
  });

  protected readonly secretHighlight = computed(() => this.ui.highlight()?.secret === true);

  protected readonly tokens = computed<readonly MapToken[]>(() => {
    const layout = this.board.layout();
    if (layout === null) {
      return [];
    }

    const range = this.range();
    const picks = this.ui.picks();

    return layout.sectors.flatMap((sector, index) =>
      sector.locations.flatMap<MapToken>((location) => {
        const terrain = terrainOf(location);
        if (terrain === null) {
          return [];
        }

        const point = tokenPoint(index, terrain);
        const lit =
          range !== null &&
          range.terrains.includes(terrain) &&
          sectorInRange(index, range.start, range.end, range.fullCircleWhenEqual);

        return [
          {
            location,
            label: locationLabel(location),
            terrain,
            x: point.x,
            y: point.y,
            lit,
            picked: picks.includes(location),
          },
        ];
      }),
    );
  });

  /**
   * What a screen reader is told the whole picture is, since the tokens alone would be a list of ids.
   *
   * The same sentence for everybody at the table, and that is the point. A caption that read the
   * viewer's hand out would be the map telling a screen-reader user something it no longer tells
   * anyone else, which is a leak with an accessibility label on it.
   */
  protected readonly caption =
    'The island: 24 locations in eight sectors, on three terrain bands — beach, forest, mountain.';

  /** A tap names a location for an accusation, and does nothing at all the rest of the time. */
  protected pick(location: string): void {
    if (this.ui.accusing()) {
      this.ui.togglePick(location);
    }
  }

  /**
   * The range being built, resolved to sector indices and to the terrains it lights.
   *
   * Shared by the segments and by the tokens so the two are read off one answer. A terrain question
   * lights its own band; a question about anywhere lights all three, because that is what it asks.
   */
  private readonly range = computed(() => {
    const layout = this.board.layout();
    const highlight = this.ui.highlight();

    if (layout === null || highlight === null) {
      return null;
    }

    const start = this.sectorOf(layout, highlight.start);
    const end = this.sectorOf(layout, highlight.end);

    if (start < 0 || end < 0) {
      return null;
    }

    const terrains: readonly Terrain[] = highlight.terrain === null ? TERRAINS : [highlight.terrain];

    return { start, end, fullCircleWhenEqual: layout.arc.startEqualsEndMeansFullCircle, terrains };
  });

  private sectorOf(layout: BoardLayoutDto, direction: Direction): number {
    return layout.sectors.findIndex((sector) => sector.direction === direction);
  }
}
