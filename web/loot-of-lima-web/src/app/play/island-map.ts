import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { BoardStore } from '../core/board-store';
import { BoardLayoutDto, Direction, Terrain } from '../core/contracts';
import { GameStore } from '../core/game-store';
import { terrainOf } from '../core/locations';
import {
  ARC_INNER_RADIUS,
  ARC_OUTER_RADIUS,
  COMPASS_RADIUS,
  SECTOR_COUNT,
  TOKEN_SIZE,
  VIEW_BOX,
  arcPath,
  boundaryAngle,
  pointAt,
  tokenPoint,
} from './map-geometry';
import { TableUi } from './table-ui';

/** What the viewer is entitled to say about one location. Nothing here is inferred. */
export type TokenState = 'mine' | 'revealed' | 'passed' | 'treasure' | 'unknown';

/** One of the 24 tokens, placed and dressed. */
interface MapToken {
  readonly location: string;
  readonly terrain: Terrain;
  readonly x: number;
  readonly y: number;
  readonly state: TokenState;
  readonly note: string;
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
 * Every state a token can be drawn in comes straight out of the view: the viewer's own hand, the
 * face-up reveal, what setup passing taught them about somebody else's hand, and at the very end
 * the treasure. There is nothing here that works anything out. A token nobody has told this client
 * about is drawn as unknown, which is the truth and is also the entire game.
 */
@Component({
  selector: 'app-island-map',
  templateUrl: './island-map.html',
  styleUrl: './island-map.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IslandMap {
  protected readonly store = inject(GameStore);
  protected readonly ui = inject(TableUi);

  private readonly board = inject(BoardStore);

  protected readonly viewBox = VIEW_BOX;
  protected readonly tokenSize = TOKEN_SIZE;

  protected readonly ready = computed(() => this.board.layout() !== null);

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
      const inner = pointAt(angle, ARC_INNER_RADIUS);
      const outer = pointAt(angle, ARC_OUTER_RADIUS);
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
   * The wedge the question being built would ask about, or nothing when none is.
   *
   * A preview and only a preview: it says which slice of the island the question covers, never how
   * many tokens are in it. The answer arrives from the server like every other answer.
   */
  protected readonly arc = computed(() => {
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

    return arcPath(start, end, layout.arc.startEqualsEndMeansFullCircle);
  });

  /** Whether the highlighted question is restricted to one terrain, which dims the other two rings. */
  protected readonly highlightTerrain = computed(() => this.ui.highlight()?.terrain ?? null);

  protected readonly secretHighlight = computed(() => this.ui.highlight()?.secret === true);

  protected readonly tokens = computed<readonly MapToken[]>(() => {
    const layout = this.board.layout();
    if (layout === null) {
      return [];
    }

    const mine = new Set(this.store.myHand());
    const revealed = new Set(this.store.view()?.publicReveal ?? []);
    const treasure = new Set(this.store.treasure() ?? []);
    const picks = this.ui.picks();

    // Only ever what passing told this viewer. `knownHand` on the viewer's own seat is their whole
    // hand, which is already covered above, so taking it again here would put every one of their
    // own tokens into the "you passed this to somebody" state.
    const passedTo = new Map<string, string>();
    for (const seat of this.store.seats()) {
      if (!seat.isViewer) {
        for (const location of seat.knownHand) {
          passedTo.set(location, seat.displayName);
        }
      }
    }

    return layout.sectors.flatMap((sector, index) =>
      sector.locations.flatMap<MapToken>((location) => {
        const terrain = terrainOf(location);
        if (terrain === null) {
          return [];
        }

        const point = tokenPoint(index, terrain);
        const state = treasure.has(location)
          ? 'treasure'
          : mine.has(location)
            ? 'mine'
            : revealed.has(location)
              ? 'revealed'
              : passedTo.has(location)
                ? 'passed'
                : 'unknown';

        return [
          {
            location,
            terrain,
            x: point.x,
            y: point.y,
            state,
            note: this.noteFor(location, state, passedTo.get(location)),
            picked: picks.includes(location),
          },
        ];
      }),
    );
  });

  /** What a screen reader is told the whole picture is, since the tokens alone would be a list of ids. */
  protected readonly caption = computed(() => {
    const held = this.store.myHand();
    return held.length === 0
      ? 'The island: 24 locations in eight sectors.'
      : `The island: 24 locations in eight sectors. You hold ${held.join(', ')}.`;
  });

  /** A tap names a location for an accusation, and does nothing at all the rest of the time. */
  protected pick(location: string): void {
    if (this.ui.accusing()) {
      this.ui.togglePick(location);
    }
  }

  private sectorOf(layout: BoardLayoutDto, direction: Direction): number {
    return layout.sectors.findIndex((sector) => sector.direction === direction);
  }

  private noteFor(location: string, state: TokenState, holder: string | undefined): string {
    switch (state) {
      case 'mine':
        return `${location} — yours`;
      case 'revealed':
        return `${location} — face up`;
      case 'treasure':
        return `${location} — the treasure`;
      case 'passed':
        return `${location} — you passed this to ${holder}`;
      default:
        return location;
    }
  }
}
