import { Injectable, computed, signal } from '@angular/core';
import { Direction, Terrain } from '../core/contracts';

/** A range as the player has it half-built, for the wedge drawn on the map. */
export interface ArcHighlight {
  readonly start: Direction;
  readonly end: Direction;
  readonly terrain: Terrain | null;
  /** A Pistol question, which is drawn differently and is never terrain-restricted. */
  readonly secret: boolean;
}

/** The two locations of an accusation, either of which may not have been chosen yet. */
export type AccusationPair = readonly [string | null, string | null];

const EMPTY: AccusationPair = [null, null];

/**
 * What the player is currently pointing at, and nothing that survives them looking away.
 *
 * The store holds what the server said. This holds what nobody has said yet: a range being built,
 * two locations half-chosen for an accusation. Both are wanted in more than one component -- the
 * question builder chooses a range and the map draws it; the map is tapped and the accusation panel
 * fills up -- and passing them through the page as inputs and outputs would put a router between
 * two things that only ever talk to each other.
 *
 * It is provided by the game page rather than at the root, so it is created with the page and dies
 * with it. A half-typed accusation is not something to carry to the next table.
 *
 * There is no game state here and no rules logic here. Nothing below knows what an answer is, and
 * the server refuses an accusation it dislikes exactly as it would if this file did not exist.
 */
@Injectable()
export class TableUi {
  private readonly _highlight = signal<ArcHighlight | null>(null);
  private readonly _picks = signal<AccusationPair>(EMPTY);
  private readonly _accusing = signal(false);

  /** The wedge to draw, or nothing when no question is being built. */
  readonly highlight = this._highlight.asReadonly();

  /**
   * The two slots of the accusation, held as a fixed pair rather than a growing list.
   *
   * A pair because both ways of filling it have to agree: tapping the map fills the next empty
   * slot, and the two selects each own one slot outright. A list that compacted itself would move
   * a location from the second select to the first the moment the first was cleared.
   */
  readonly picks = this._picks.asReadonly();

  /** Whether the accusation panel is open, which is also what makes the map tappable. */
  readonly accusing = this._accusing.asReadonly();

  readonly named = computed<readonly string[]>(() =>
    this._picks().filter((location): location is string => location !== null),
  );

  /** Two different locations. The server checks this too; the button is disabled so it need not. */
  readonly canAccuse = computed(() => {
    const [first, second] = this._picks();
    return first !== null && second !== null && first !== second;
  });

  setHighlight(highlight: ArcHighlight | null): void {
    this._highlight.set(highlight);
  }

  /**
   * Opens or closes the accusation panel, always with an empty pair.
   *
   * Clearing on the way in as well as on the way out is deliberate. Reopening the panel on tokens
   * chosen minutes ago and forgotten is how somebody accuses with one location they meant and one
   * they do not remember choosing, and an accusation cannot be taken back.
   */
  setAccusing(accusing: boolean): void {
    this._picks.set(EMPTY);
    this._accusing.set(accusing);
  }

  /**
   * Names a location from the map: fills the first empty slot, unnames one already chosen, and
   * rolls the older of the two off once both are full.
   *
   * Rolling rather than refusing. A third tap is somebody changing their mind, and a map that goes
   * dead until they find the token they no longer want and tap it again is worse than picking up
   * where they clearly meant to.
   */
  togglePick(location: string): void {
    this._picks.update(([first, second]) => {
      if (first === location) {
        return [null, second];
      }

      if (second === location) {
        return [first, null];
      }

      if (first === null) {
        return [location, second];
      }

      return second === null ? [first, location] : [second, location];
    });
  }

  /** Names a location in one particular slot, which is what a select does. */
  setPick(slot: 0 | 1, location: string | null): void {
    this._picks.update((picks) => {
      const pair: [string | null, string | null] = [picks[0], picks[1]];
      pair[slot] = location;

      // One location cannot be both halves of an accusation, so naming it here gives up the other
      // slot rather than leaving a pair the server would only refuse.
      if (location !== null && pair[1 - slot] === location) {
        pair[1 - slot] = null;
      }

      return pair;
    });
  }

  clearPicks(): void {
    this._picks.set(EMPTY);
  }
}
