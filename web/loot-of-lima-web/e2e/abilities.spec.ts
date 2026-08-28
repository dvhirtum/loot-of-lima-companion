import { expect, test } from '@playwright/test';
import { askSecretly, deal, field, leave, openTable, seatCard, sitDown, spendAbility, takeTokens } from './fixtures/table';

/**
 * The three ability tokens.
 *
 * Two of them do nothing but get spent, which is exactly the claim worth pinning: the app does not
 * model the roll, so Barrel and Shovel are bookkeeping and must not grow a mechanic by accident.
 * That they are marked used *for everybody* is a rule -- the tokens sit face up on the player board.
 *
 * The Pistol is the one with real behaviour, and it needs three humans to test properly. The
 * redaction is only observable from a seat that was not party to the question, and every other seat
 * at a table of one is a bot with no screen to look at.
 */
test.describe('Spending an ability', () => {
  test('Should_mark_the_barrel_spent_for_the_whole_table_when_it_is_used', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace]);

    // Act
    await spendAbility(ada.page, 'Barrel');

    // Assert
    // Gone from Ada's own seat card and from Grace's copy of it. The button stays on screen and
    // names itself spent, because what is gone is as much use to know as what is left.
    await expect(seatCard(ada.page, 0)).toContainText('Shovel, Pistol');
    await expect(seatCard(grace.page, 0)).toContainText('Shovel, Pistol');
    await expect(seatCard(grace.page, 0)).not.toContainText('Barrel');

    await expect(ada.page.getByRole('button', { name: 'Barrel, spent' })).toBeDisabled();
    await expect(grace.page.locator('.log .entry').first()).toContainText('Ada spent the Barrel');

    // Barrel and Shovel act on dice this app does not model, so being spent is their entire effect:
    // the turn is still Ada's and the question panel is unchanged.
    await expect(ada.page.getByRole('heading', { name: 'Ask a question' })).toBeVisible();
    await expect(ada.page.getByText('Your turn', { exact: true })).toBeVisible();

    await leave([ada, grace]);
  });

  test('Should_turn_the_panel_into_a_secret_one_when_the_pistol_is_spent', async ({ browser }) => {
    // Arrange
    // Only the wording and the button change. The Pistol lifts the roll, not the shapes a question
    // may take, so the terrain field stays exactly where it was — losing it here would be the app
    // inventing a restriction the game does not have.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada]);

    // Act
    await spendAbility(ada.page, 'Pistol');

    // Assert
    await expect(ada.page.getByRole('heading', { name: 'Ask a secret question' })).toBeVisible();
    await expect(field(ada.page, 'Terrain')).toBeEnabled();
    await expect(ada.page.getByRole('button', { name: 'Ask secretly' })).toBeVisible();

    await leave([ada]);
  });

  test('Should_answer_within_one_terrain_when_the_secret_question_names_one', async ({ browser }) => {
    // Arrange
    // Grace holds F1 F2 F3 F8 by the book, so the whole island answers 4 unrestricted and 0 for
    // mountains — which is what makes this worth asserting: a terrain dropped anywhere between the
    // panel and the engine comes back as 4, and the log would misquote the question as well.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace]);

    // Act
    await spendAbility(ada.page, 'Pistol');
    await askSecretly(ada.page, { of: 'Grace', from: 'N', upTo: 'N', terrain: 'Mountain' });

    // Assert
    for (const page of [ada.page, grace.page]) {
      const entry = page.locator('.log .entry').first();
      await expect(entry).toContainText('Secretly, Ada asked Grace');
      await expect(entry).toContainText('mountain locations');
      await expect(entry.locator('.answer')).toHaveText('0');
    }

    await leave([ada, grace]);
  });

  test('Should_tell_only_the_two_of_them_when_a_secret_question_is_asked', async ({ browser }) => {
    // Arrange
    // Three humans, because this is the assertion the whole hidden-information design exists for and
    // it takes a third screen to make it. Grace holds F1 F2 F3 F8 by the book, so a question about
    // the whole island answers 4 — a number Hedy must not be able to see anywhere.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    const hedy = await sitDown(browser, roomCode, 'Hedy', 2);
    await deal(ada.page, { book: 1, number: 1 });
    await takeTokens([ada, grace, hedy]);

    // Act
    await spendAbility(ada.page, 'Pistol');
    await askSecretly(ada.page, { of: 'Grace', from: 'N', upTo: 'N' });

    // Assert
    // The asker and the target get the question and the answer.
    for (const page of [ada.page, grace.page]) {
      const entry = page.locator('.log .entry').first();
      await expect(entry).toContainText('Secretly, Ada asked Grace');
      await expect(entry.locator('.answer')).toHaveText('4');
    }

    // Everybody else gets a different entry with no property for the range or the answer at all --
    // redaction by type, not by a blanked field. There is nowhere on this line for a number to be.
    const seen = hedy.page.locator('.log .entry').first();
    await expect(seen).toContainText('Ada asked Grace a secret question');
    await expect(seen.locator('.answer')).toHaveCount(0);
    await expect(seen).not.toContainText('4');

    await leave([ada, grace, hedy]);
  });
});
