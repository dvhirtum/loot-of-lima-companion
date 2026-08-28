import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { locationLabel, terrainClass } from '../core/locations';

/**
 * One location token, coloured by its terrain and written the way the box writes it.
 *
 * Takes the server's id and shows the label, which is why almost nothing else in the app has to
 * know the two differ: every token a player sees comes through here.
 */
@Component({
  selector: 'app-location-token',
  template: `<span class="token" [class]="css()">{{ label() }}</span>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LocationToken {
  readonly location = input.required<string>();

  protected readonly css = computed(() => terrainClass(this.location()));

  protected readonly label = computed(() => locationLabel(this.location()));
}
