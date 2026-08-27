import { Injectable } from '@angular/core';
import { JoinResponse } from './contracts';

/** What this browser holds to prove it owns a seat at one particular table. */
export interface PlayerIdentity {
  readonly playerToken: string;
  readonly playerId: string;
  readonly displayName: string;
}

const PREFIX = 'lol.player.';

/**
 * Where the player token lives between page loads.
 *
 * The token is a bearer credential -- whoever holds it holds the seat and the hand that comes with
 * it -- so it stays in this browser and is never put in a URL, where it would end up in history,
 * in a shared link and in every server log along the way.
 *
 * Stored per room code rather than one at a time, because a phone that joins a second table has
 * not stopped being welcome at the first, and one slot would silently evict a seat somebody is
 * still sitting in.
 */
@Injectable({ providedIn: 'root' })
export class PlayerIdentityStore {
  find(roomCode: string): PlayerIdentity | null {
    const raw = PlayerIdentityStore.read(PREFIX + key(roomCode));
    if (raw === null) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as Partial<PlayerIdentity>;
      return parsed.playerToken && parsed.playerId
        ? { playerToken: parsed.playerToken, playerId: parsed.playerId, displayName: parsed.displayName ?? '' }
        : null;
    } catch {
      // Written by an older build, or half-written by a tab that died mid-set. Either way it is not
      // an identity, and throwing out of a getter would take the whole page down over a stale string.
      return null;
    }
  }

  remember(roomCode: string, joined: JoinResponse, displayName: string): PlayerIdentity {
    const identity: PlayerIdentity = {
      playerToken: joined.playerToken,
      playerId: joined.playerId,
      displayName,
    };

    PlayerIdentityStore.write(PREFIX + key(roomCode), JSON.stringify(identity));
    return identity;
  }

  forget(roomCode: string): void {
    PlayerIdentityStore.write(PREFIX + key(roomCode), null);
  }

  /**
   * Every read of `localStorage`, and the only place one may live.
   *
   * It is not merely empty in a private window or with site data blocked: reaching for it throws,
   * and so does using it. Both have to be inside the same guard, because a `getItem` outside one
   * fails just as hard as the property access -- which is exactly the shape this used to have.
   * A failure here means "no identity", and the player joins again.
   */
  private static read(name: string): string | null {
    try {
      return globalThis.localStorage?.getItem(name) ?? null;
    } catch {
      return null;
    }
  }

  /** The same guard for writing. A null value removes the entry. */
  private static write(name: string, value: string | null): void {
    try {
      if (value === null) {
        globalThis.localStorage?.removeItem(name);
      } else {
        globalThis.localStorage?.setItem(name, value);
      }
    } catch {
      // Out of quota, or storage disabled. The identity still works for this page load, which is
      // enough to play; only the reconnect after a refresh is lost.
    }
  }
}

/**
 * Room codes are scanned in upper case from the QR link and typed by hand on the home page, so the
 * same seat has to be reachable by either spelling.
 */
function key(roomCode: string): string {
  return roomCode.toUpperCase();
}
