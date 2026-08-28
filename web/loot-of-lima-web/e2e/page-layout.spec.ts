import { Page, expect, test } from '@playwright/test';
import { askQuestion, deal, leave, openTable, sitDown, takeTokens } from './fixtures/table';

/**
 * Where the panels sit, on a phone and on a laptop.
 *
 * There is one layout now: the panels are the grid's own children in the order a player uses them,
 * and the width decides only how many columns they are poured into. So both halves of this file
 * assert the same claim about the same sequence, which is why they live together.
 *
 * That claim is the loop — island, ask, answer — and it was broken at both sizes for the same
 * reason. Stacked into one column, the ability bar sat between the island and the builder; poured
 * into two, the island's whole height plus the ability bar sat above the builder in the left-hand
 * one. Either way, asking meant scrolling down to choose a range, back up to check the arc against
 * the island, and down again to press Ask.
 *
 * Vertical and horizontal position is the only honest way to assert this. A DOM-order check would
 * pass while the grid painted the panels somewhere else entirely — and it is the grid, not the
 * document, that decides which column a panel lands in.
 */
const PHONE = { width: 375, height: 667 };
const LAPTOP = { width: 1280, height: 900 };

/** Where a panel starts, in CSS pixels, and how big it is. */
async function boxOf(page: Page, selector: string): Promise<{ x: number; y: number; width: number }> {
  const box = await page.locator(selector).boundingBox();
  if (box === null) {
    throw new Error(`${selector} is not on the page, so it has no position to compare.`);
  }

  return box;
}

test.describe('The game page on a phone', () => {
  test.use({ viewport: PHONE });

  test('Should_run_island_then_question_then_answer_down_the_page_when_it_is_your_turn', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();

    // Act
    // Asked so the log is not empty: the claim is about where the answer lands, and an empty panel
    // would be in the right place while saying nothing.
    await askQuestion(ada.page, { of: 'Bot 3', from: 'N', upTo: 'N', terrain: 'Forest' });

    // Assert
    const island = (await boxOf(ada.page, 'app-island-map')).y;
    const question = (await boxOf(ada.page, 'app-question-builder')).y;
    const history = (await boxOf(ada.page, 'app-history-panel')).y;
    const hand = (await boxOf(ada.page, 'app-my-hand-panel')).y;
    const abilities = (await boxOf(ada.page, 'app-ability-bar')).y;

    // The loop a player repeats every turn, in the order they repeat it.
    expect(island).toBeLessThan(question);
    expect(question).toBeLessThan(history);

    // Then what they can still act on: spent at most three times a game, so after the loop.
    expect(history).toBeLessThan(abilities);

    // The tokens panel is last, because it is the only one nothing is done to and the seat strip
    // has already printed this seat's hand at the top of the page.
    expect(abilities).toBeLessThan(hand);

    await leave([ada]);
  });

  test('Should_reach_the_question_within_one_screen_of_the_island_when_it_is_your_turn', async ({ browser }) => {
    // Arrange
    // The arc preview is why the island is drawn rather than pictured, so the control that sets the
    // range has to be reachable from it without hunting. This is deliberately a claim about the
    // *fields*, not about the Ask button: on a 375x667 phone the button sits a little below the
    // fold whatever the map is shrunk to, and a spec that pretended otherwise would only be
    // satisfiable by making the island too small to read.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    await ada.page.locator('app-island-map').scrollIntoViewIfNeeded();

    // Assert
    const map = (await boxOf(ada.page, 'app-island-map')).y;
    const question = (await boxOf(ada.page, 'app-question-builder')).y;

    expect(question - map).toBeLessThan(PHONE.height);

    await leave([ada]);
  });
});

test.describe('The game page on a laptop', () => {
  test.use({ viewport: LAPTOP });

  test('Should_put_the_question_and_the_log_beside_the_island_when_there_is_room', async ({ browser }) => {
    // Arrange
    // The same three panels as the phone spec, and the same claim: the loop has to be in one place.
    // Here that means beside the island rather than under it — with the builder in the left-hand
    // column it sat below the map's full height and the ability bar, which is a screen and a half
    // down on a laptop and exactly the scrolling the phone ordering was written to stop.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();

    // Act
    const island = await boxOf(ada.page, 'app-island-map');
    const question = await boxOf(ada.page, 'app-question-builder');
    const history = await boxOf(ada.page, 'app-history-panel');

    // Assert
    // Beside, not below. This is the assertion the old layout failed: the builder shared the
    // island's column and started well past the bottom of it.
    expect(question.x).toBeGreaterThan(island.x + island.width - 1);
    expect(history.x).toBeGreaterThan(island.x + island.width - 1);

    // The island spans both of their rows, so the builder starts level with it and the log follows
    // directly underneath in the same column.
    expect(Math.abs(question.y - island.y)).toBeLessThan(2);
    expect(Math.abs(history.x - question.x)).toBeLessThan(2);
    expect(history.y).toBeGreaterThan(question.y);

    // And all three are one screen, which is the whole point of putting them together.
    expect(history.y - island.y).toBeLessThan(LAPTOP.height);

    await leave([ada]);
  });
});
