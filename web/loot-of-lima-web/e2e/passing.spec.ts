import { expect, test } from '@playwright/test';
import { deal, leave, openTable, ownHand, sitDown, statedHandSize, takeTokens } from './fixtures/table';

/**
 * The five-player pass: one token right, then one token left.
 *
 * Nothing else in this suite reaches it, and the reason is easy to miss. Every five-player spec here
 * plays a printed scenario, and a scenario deal never passes -- `Passing.IsRequired` keys off
 * `DealSourceKind`, because the books' published answers describe the hands exactly as dealt. So the
 * only five-player game that passes tokens is a shuffled one, which is this file and nowhere else.
 *
 * It is also the only place three things appear on screen at all:
 *
 * - a seat passing *twice*, so the panel renders more than one row;
 * - the words "right" and "left", since a four-player pass goes straight across;
 * - `tokenPassOccurred`, the redacted form -- a pass a viewer was not party to, with no property for
 *   the location. The four-player spec has one human, so every other party to a pass is a bot with
 *   no screen, and the redacted form is never rendered anywhere.
 *
 * `Passing.Plan` is thoroughly unit-tested and `Projector` has its own tests for the redaction. What
 * neither can say is that the page shows it.
 */
test.describe('Passing at a five-player table', () => {
  test('Should_pass_twice_in_both_directions_when_the_deal_is_shuffled', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    await deal(ada.page, null);
    await takeTokens([ada]);

    // Assert
    // Four rows for this seat: two given and two received. A four-player table would show two.
    const panel = ada.page.locator('.card').filter({ hasText: 'Pass a token' });
    await expect(panel).toBeVisible();
    await expect(panel.locator('p.row')).toHaveCount(4);
    await expect(panel).toContainText('right');
    await expect(panel).toContainText('left');

    await leave([ada]);
  });

  test('Should_keep_every_hand_at_four_when_the_passes_are_done', async ({ browser }) => {
    // Arrange
    // Everybody gives two and receives two, so the count never moves even though the contents do.
    // A pass that lost or duplicated a token would show up here and nowhere else in this suite.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, null);
    await takeTokens([ada]);

    // Act
    await ada.page.getByRole('button', { name: 'Done passing' }).click();

    // Assert
    await expect(ada.page.getByText('Announce your fewest terrain')).toBeVisible();
    expect(await ownHand(ada.page)).toHaveLength(4);
    for (let seat = 0; seat < 5; seat += 1) {
      expect(await statedHandSize(ada.page, seat)).toBe(4);
    }

    await leave([ada]);
  });

  test('Should_name_the_token_only_to_the_two_seats_involved_when_a_pass_is_logged', async ({ browser }) => {
    // Arrange
    // Three humans, because the redacted form needs somebody with a screen who was not party to the
    // pass. A token learned by passing is real knowledge about another hand -- it is the only thing
    // the passing phase gives you -- so the giver and receiver must see the location and nobody else
    // may. Ten passes happen; Ada is party to exactly four of them at any table size of five.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 5);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    const grace = await sitDown(browser, roomCode, 'Grace', 1);
    const alan = await sitDown(browser, roomCode, 'Alan', 2);

    await deal(ada.page, null);
    await takeTokens([ada, grace, alan]);

    // Act
    for (const player of [ada, grace, alan]) {
      await player.page.getByRole('button', { name: 'Done passing' }).click();
    }

    // Assert
    await expect(ada.page.getByText('Announce your fewest terrain')).toBeVisible();

    const passes = ada.page.locator('.log .entry').filter({ hasText: 'passed' });
    const named = passes.filter({ has: ada.page.locator('.token') });
    const redacted = passes.filter({ hasText: 'passed a token to' });

    await expect(passes).toHaveCount(10);
    await expect(named).toHaveCount(4);
    await expect(redacted).toHaveCount(6);

    // Not merely a different phrasing: the redacted entry has no property for the location, so
    // there is no token element in it to find.
    await expect(redacted.locator('.token')).toHaveCount(0);

    // And the four Ada can read are the four she was party to.
    for (const entry of await named.allInnerTexts()) {
      expect(entry).toMatch(/Ada/);
    }

    await leave([ada, grace, alan]);
  });
});
