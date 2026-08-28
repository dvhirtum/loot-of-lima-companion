import { Injectable, signal } from '@angular/core';
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
  /**
   * The identities handed out during this page load, whether or not storage kept them.
   *
   * With storage unavailable -- a private window, or site data blocked -- reading it back comes up
   * empty, so a player who had just joined successfully would arrive at their own table as a
   * spectator with no hand. Holding the identity here as well makes storage what it actually is:
   * what survives a refresh, not what makes the seat work at all.
   *
   * A signal, and holding a new Map on every write rather than mutating one, because joining is no
   * longer a page of its own. The name is asked for at the seat, so `remember` now runs on the very
   * page whose connection has to pick the token up -- and the effect that reconnects reads this
   * through a `computed`. A plain Map is invisible to that: the token would be written down and the
   * socket would stay the spectator's, so the seat could only ever be refused.
   */
  private readonly inMemory = signal<ReadonlyMap<string, PlayerIdentity>>(new Map());

  find(roomCode: string): PlayerIdentity | null {
    // Read first and unconditionally, before the branch that may not need it. This is the only
    // reactive read in the method -- `localStorage` notifies nobody -- so a caller inside a
    // `computed` is subscribed to `remember` and `forget` however the lookup below turns out.
    const thisPageLoad = this.inMemory().get(key(roomCode)) ?? null;

    const raw = PlayerIdentityStore.read(PREFIX + key(roomCode));
    if (raw === null) {
      return thisPageLoad;
    }

    try {
      const parsed = JSON.parse(raw) as Partial<PlayerIdentity>;
      if (parsed.playerToken && parsed.playerId) {
        return { playerToken: parsed.playerToken, playerId: parsed.playerId, displayName: parsed.displayName ?? '' };
      }
    } catch {
      // Written by an older build, or half-written by a tab that died mid-set. Either way it is not
      // an identity, and throwing out of a getter would take the whole page down over a stale string.
    }

    // Unreadable is the same as absent, and this page load may still know who this is.
    return thisPageLoad;
  }

  remember(roomCode: string, joined: JoinResponse, displayName: string): PlayerIdentity {
    const identity: PlayerIdentity = {
      playerToken: joined.playerToken,
      playerId: joined.playerId,
      displayName,
    };

    this.inMemory.update((held) => new Map(held).set(key(roomCode), identity));
    PlayerIdentityStore.write(PREFIX + key(roomCode), JSON.stringify(identity));
    return identity;
  }

  forget(roomCode: string): void {
    this.inMemory.update((held) => {
      const next = new Map(held);
      next.delete(key(roomCode));
      return next;
    });

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
      // Out of quota, or storage disabled. `remember` has already put the identity in `inMemory`,
      // so the seat works for as long as the tab is open and only surviving a refresh is lost.
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
