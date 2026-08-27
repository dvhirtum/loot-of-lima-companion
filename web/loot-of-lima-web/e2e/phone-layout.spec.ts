import { Page, expect, test } from '@playwright/test';
import { askQuestion, deal, leave, openTable, sitDown, takeTokens } from './fixtures/table';

/**
 * The order the panels come in on a phone.
 *
 * A phone is where players actually sit -- they scan a code and play on the device in their hand --
 * and the two-column layout that reads well on a laptop stacks into one column there, in an order
 * that is nobody's on purpose. It put the ability bar between the island and the question builder,
 * so asking meant scrolling down to choose a range, back up to check the arc, and down again to
 * press Ask; the answer then arrived in the history panel below everything else.
 *
 * Vertical position is the only honest way to assert this. A DOM-order check would pass while CSS
 * `order` put the panels somewhere else entirely, which is exactly the mechanism in use.
 */
const PHONE = { width: 375, height: 667 };

/** How far down the page a panel starts, in CSS pixels. */
async function topOf(page: Page, selector: string): Promise<number> {
  const box = await page.locator(selector).boundingBox();
  if (box === null) {
    throw new Error(`${selector} is not on the page, so it has no position to compare.`);
  }

  return box.y;
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
    const island = await topOf(ada.page, 'app-island-map');
    const question = await topOf(ada.page, 'app-question-builder');
    const history = await topOf(ada.page, 'app-history-panel');
    const abilities = await topOf(ada.page, 'app-ability-bar');

    // The loop a player repeats every turn, in the order they repeat it.
    expect(island).toBeLessThan(question);
    expect(question).toBeLessThan(history);

    // Spent at most three times in a game, so it comes after the loop rather than inside it.
    expect(history).toBeLessThan(abilities);

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
    const map = await topOf(ada.page, 'app-island-map');
    const question = await topOf(ada.page, 'app-question-builder');

    expect(question - map).toBeLessThan(PHONE.height);

    await leave([ada]);
  });

  test('Should_keep_the_two_columns_when_there_is_room_for_them', async ({ browser }) => {
    // Arrange
    // The phone ordering is a media query, so the laptop layout is the other half of the claim: the
    // island and the history sit side by side rather than one below the other.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await ada.page.setViewportSize({ width: 1280, height: 900 });
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    const island = await ada.page.locator('app-island-map').boundingBox();
    const history = await ada.page.locator('app-history-panel').boundingBox();

    // Assert
    if (island === null || history === null) {
      throw new Error('Both panels must be on the page.');
    }

    expect(history.x).toBeGreaterThan(island.x + island.width - 1);

    await leave([ada]);
  });
});
