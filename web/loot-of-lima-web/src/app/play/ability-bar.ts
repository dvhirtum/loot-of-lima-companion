import { ChangeDetectionStrategy, Component, computed, inject, output } from '@angular/core';
import { AbilityType } from '../core/contracts';
import { GameStore } from '../core/game-store';

/** One ability token as this seat currently holds it. */
interface AbilityChoice {
  readonly type: AbilityType;
  readonly spent: boolean;
  readonly blurb: string;
}

const BLURBS: Readonly<Record<AbilityType, string>> = {
  Barrel: 'Re-roll any number of dice.',
  Shovel: 'Treat one die as a terrain of your choice.',
  Pistol: 'Ask one question in secret, at any range the dice could have given you.',
};

/**
 * The three ability tokens, spent once each per game.
 *
 * Two of the three do nothing here and say so. Barrel and Shovel act on the physical dice, and this
 * app deliberately does not model a roll -- so spending them is bookkeeping, which is exactly the
 * job the app was built to take over. Pretending otherwise would mean asking every player what they
 * rolled on every turn to buy a validation the table already does for itself.
 *
 * The Pistol is the exception, because it is a rule about the app rather than about the dice: it
 * unlocks the secret question, and the question builder becomes a secret one the moment it is spent.
 */
@Component({
  selector: 'app-ability-bar',
  template: `
    <section class="card stack">
      <h3>Abilities</h3>

      <div class="abilities">
        @for (ability of abilities(); track ability.type) {
          <button
            type="button"
            class="button ability"
            [class.ability--spent]="ability.spent"
            [disabled]="ability.spent || !canSpend()"
            [attr.aria-label]="ability.type + (ability.spent ? ', spent' : '')"
            (click)="spend.emit(ability.type)"
          >
            <span class="ability__name">{{ ability.type }}</span>
            <span class="ability__blurb small">{{ ability.spent ? 'Spent.' : ability.blurb }}</span>
          </button>
        }
      </div>

      @if (armed()) {
        <p class="small">Pistol spent. Your question this turn is a secret one.</p>
      } @else if (!store.isMyTurn()) {
        <p class="small muted">Abilities are spent on your own turn, while you are building a question.</p>
      } @else {
        <p class="small muted">
          Barrel and Shovel act on the dice, which stay on the table — the app only records that they are gone.
        </p>
      }
    </section>
  `,
  styles: `
    .abilities {
      display: grid;
      gap: 0.5rem;
      grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
    }

    .ability {
      flex-direction: column;
      align-items: flex-start;
      gap: 0.15rem;
      text-align: left;
      padding: 0.5rem 0.7rem;
    }

    .ability__name {
      font-weight: 600;
    }

    .ability__blurb {
      color: var(--parchment-dim);
      line-height: 1.3;
    }

    /* Spent tokens stay on screen, struck through: what is gone is as much use to know as what is left. */
    .ability--spent {
      opacity: 0.45;
      border-style: dashed;
    }

    .ability--spent .ability__name {
      text-decoration: line-through;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AbilityBar {
  protected readonly store = inject(GameStore);

  readonly spend = output<AbilityType>();

  /** Always all three, in the order they are printed on the player board. */
  protected readonly abilities = computed<readonly AbilityChoice[]>(() => {
    const remaining = new Set(this.store.mySeat()?.abilitiesRemaining ?? []);

    return (['Barrel', 'Shovel', 'Pistol'] as const).map((type) => ({
      type,
      spent: !remaining.has(type),
      blurb: BLURBS[type],
    }));
  });

  protected readonly armed = computed(() => this.store.mySeat()?.pistolArmed === true);

  /**
   * Whether anything can be spent right now.
   *
   * The seat's own turn, and not eliminated. Both are the server's to enforce and it does; the
   * button is disabled so a player is not invited to make a move that will only come back refused.
   * A spent one is disabled by its own flag beside this: an ability token is gone for the rest of
   * the game, and a button that invites a player to spend it again is a button that lies.
   */
  protected readonly canSpend = computed(() => {
    const seat = this.store.mySeat();
    return seat !== null && !seat.eliminated && this.store.isMyTurn();
  });
}
