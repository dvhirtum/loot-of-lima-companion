import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ApiClient } from '../../core/api-client';
import { BoardLayoutDto } from '../../core/contracts';
import { IslandMap } from '../island-map';
import { TableUi } from '../table-ui';

/**
 * What the island says when the board layout never arrives.
 *
 * `GET /api/board` is fetched once for the life of the app, so a failure here is a page that does
 * not fix itself — and it takes more than the picture with it. The accusation panel builds its two
 * selects from the same 24 locations, so a silent failure leaves a player unable to accuse, with
 * nothing on screen saying why and nothing to press.
 *
 * There is a sharper reason than the empty selects, found while fixing this: a resource in the
 * error state does not hand back its default from `value()`, it re-throws the loader's error. So
 * an unguarded read does not degrade to a blank island — it throws out of the render.
 */
describe('IslandMap board failure', () => {
  const LAYOUT: BoardLayoutDto = {
    sectors: [
      { direction: 'N', locations: ['B1', 'F1', 'M1'] },
      { direction: 'NE', locations: ['B2', 'F2', 'M2'] },
      { direction: 'E', locations: ['B3', 'F3', 'M3'] },
      { direction: 'SE', locations: ['B4', 'F4', 'M4'] },
      { direction: 'S', locations: ['B5', 'F5', 'M5'] },
      { direction: 'SW', locations: ['B6', 'F6', 'M6'] },
      { direction: 'W', locations: ['B7', 'F7', 'M7'] },
      { direction: 'NW', locations: ['B8', 'F8', 'M8'] },
    ],
    arc: { startInclusive: true, endExclusive: true, startEqualsEndMeansFullCircle: true },
  };

  function configure(board: () => Promise<BoardLayoutDto>): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [IslandMap],
      providers: [TableUi, { provide: ApiClient, useValue: { board } }],
    });
  }

  async function render(): Promise<ComponentFixture<IslandMap>> {
    const fixture = TestBed.createComponent(IslandMap);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function text(fixture: ComponentFixture<IslandMap>): string {
    return ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');
  }

  beforeEach(() => {
    configure(() => Promise.resolve(LAYOUT));
  });

  it('Should_offer_another_attempt_when_the_board_could_not_be_fetched', async () => {
    // Arrange
    configure(() => Promise.reject(new Error('offline')));

    // Act
    const fixture = await render();

    // Assert
    // Rendering at all is half the claim: an unguarded value() read throws out of this line.
    expect(text(fixture)).toContain('could not be loaded');
    expect(text(fixture)).not.toContain('Drawing the island');

    const retry = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
      (candidate) => (candidate.textContent ?? '').trim() === 'Try again',
    );
    expect(retry).toBeDefined();
  });

  it('Should_draw_the_island_when_the_board_arrives', async () => {
    // Arrange
    // The other side, so the failure branch cannot be mistaken for the only one that renders.

    // Act
    const fixture = await render();

    // Assert
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('[data-location]')).toHaveLength(24);
    expect(text(fixture)).not.toContain('could not be loaded');
  });

  it('Should_stop_the_page_scrolling_when_a_location_is_named_with_the_space_bar', async () => {
    // Arrange
    // The tokens are `g` elements wearing role="button", and Space scrolls anything focusable that
    // is not a real button. An accusation names two of them, so an unsuppressed first tap scrolls
    // the island out from under the second.
    const fixture = await render();
    const ui = TestBed.inject(TableUi);
    ui.setAccusing(true);
    fixture.detectChanges();

    const token = (fixture.nativeElement as HTMLElement).querySelector('[data-location="B1"]');
    const space = new KeyboardEvent('keydown', { key: ' ', code: 'Space', bubbles: true, cancelable: true });

    // Act
    token!.dispatchEvent(space);
    fixture.detectChanges();

    // Assert
    expect(space.defaultPrevented).toBe(true);
    expect(ui.picks()).toEqual(['B1', null]);
  });
});
