import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { terrainClass } from '../core/locations';

/** One location token, coloured by its terrain. */
@Component({
  selector: 'app-location-token',
  template: `<span class="token" [class]="css()">{{ location() }}</span>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LocationToken {
  readonly location = input.required<string>();

  protected readonly css = computed(() => terrainClass(this.location()));
}
