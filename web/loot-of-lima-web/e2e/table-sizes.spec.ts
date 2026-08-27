import { expect, test } from '@playwright/test';
import {
  announceFewest,
  deal,
  field,
  faceUpTokens,
  leave,
  openTable,
  ownHand,
  seatCard,
  sitDown,
  statedHandSize,
  takeTokens,
} from './fixtures/table';

/**
 * Three and four players, asserted by shape and never by value.
 *
 * That is a settled decision rather than a gap. The printed scenarios are five-player only, so a
 * smaller table always plays a generated deal -- and `POST /api/games` seeds from
 * `Random.Shared.Next()` with no seed field on the request, so no hand, answer or announcement
 * repeats between runs. Adding a test-only seed would put a hook in the wire contract to buy
 * assertions the scenario specs already make.
 *
 * What is stable is the shape of the deal: 22 locations dealt evenly with the remainder face up,
 * and passing that depends on the table size. That is what these check.
 */
test.describe('A three-player table', () => {
  test('Should_deal_seven_tokens_each_and_skip_passing_when_three_are_playing', async ({ browser }) => {
    // Arrange
    // Three hands of seven leaves one over, and with nothing to pass the deal settles the table by
    // itself -- there is no Passing phase to skip, because none is ever entered.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 3);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    await deal(ada.page, null);
    await takeTokens([ada]);

    // Assert
    await expect(ada.page.getByText('Announce your fewest terrain')).toBeVisible();
    await expect(ada.page.getByText('Pass a token')).toHaveCount(0);

    expect(await ownHand(ada.page)).toHaveLength(7);
    for (let seat = 0; seat < 3; seat += 1) {
      expect(await statedHandSize(ada.page, seat)).toBe(7);
    }

    // Exactly one, not "more than none". Twenty-two locations dealt three ways leaves one over, and
    // an assertion that only says the words "Face up" appear on the page is satisfied by a section
    // heading -- it would pass while the reveal itself was empty, or wrong, or the whole hand.
    expect(await faceUpTokens(ada.page)).toHaveLength(1);

    await leave([ada]);
  });

  test('Should_have_every_seat_announce_before_play_begins_when_the_deal_is_shuffled', async ({ browser }) => {
    // Arrange
    // A generated deal prescribes nothing, so the announcement is really made rather than replayed:
    // the human chooses and the bots choose for themselves, and play cannot start until all three
    // have.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 3);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, null);
    await takeTokens([ada]);

    // Act
    const announced = await announceFewest(ada.page);

    // Assert
    await expect(ada.page.getByText('InProgress')).toBeVisible();
    await expect(seatCard(ada.page, 0)).toContainText(`fewest ${announced.toLowerCase()}`);
    for (let seat = 0; seat < 3; seat += 1) {
      await expect(seatCard(ada.page, seat)).toContainText('fewest');
    }

    await leave([ada]);
  });

  test('Should_offer_only_truthful_terrains_when_a_seat_is_asked_to_announce', async ({ browser }) => {
    // Arrange
    // Structure, not values: a shuffled deal is seeded from `Random.Shared.Next()`, so which terrain
    // is fewest differs every run. What never differs is that the offer matches the hand — and that
    // the seat is still asked, however little choice it has.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 3);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, null);
    await takeTokens([ada]);

    const panel = ada.page.locator('.card').filter({ hasText: 'Announce your fewest terrain' });
    const buttons = panel.getByRole('button');
    await expect(buttons.first()).toBeVisible();

    // Act
    // All three stay on screen with the viewer's own count on each, so a player can check the offer
    // against their own hand rather than taking the app's word for it.
    const captions = await buttons.allInnerTexts();
    const held = captions.map((caption) => {
      const [, terrain, count] = /^(\w+)\s*\((\d+)\)/.exec(caption.replace(/\s+/g, ' ').trim()) ?? [];
      return { terrain: terrain ?? '', count: Number(count ?? Number.NaN) };
    });

    // Assert
    expect(held).toHaveLength(3);

    const fewest = held.reduce((best, next) => (next.count < best.count ? next : best)).count;
    for (const [index, choice] of held.entries()) {
      const truthful = choice.count === fewest;
      const button = buttons.nth(index);

      // The engine refuses an untruthful announcement, so an enabled button here would be a button
      // whose only outcome is a refusal naming an internal seat index.
      if (truthful) {
        await expect(button, `${choice.terrain} is a minimum and should be offered`).toBeEnabled();
      } else {
        await expect(button, `${choice.terrain} is not a minimum and must not be offered`).toBeDisabled();
      }
    }

    // Still asked, whatever the hand. Announcing a forced answer by itself would leave the tied
    // seats as the only ones anybody waits for, which tells the table exactly what the rulebook
    // says a tie must not.
    await expect(ada.page.getByText('Announcing')).toBeVisible();

    await leave([ada]);
  });

  test('Should_not_offer_the_scenario_picker_when_fewer_than_five_are_playing', async ({ browser }) => {
    // Arrange
    // The books simulate a five-player game and nothing else, so a smaller table always plays a
    // generated deal. Offering a picker that could only be refused would be worse than saying so.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 3);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    const lobby = ada.page.locator('.card').filter({ hasText: 'Start the game' });

    // Assert
    await expect(lobby).toBeVisible();
    await expect(lobby.getByRole('button', { name: 'Play a printed scenario' })).toHaveCount(0);
    await expect(field(ada.page, 'Book')).toHaveCount(0);
    await expect(lobby).toContainText('The official scenarios are five-player only');

    // Still startable, because a shuffled deal needs nothing chosen.
    await expect(lobby.getByRole('button', { name: 'Deal and begin' })).toBeEnabled();

    await leave([ada]);
  });
});

test.describe('A four-player table', () => {
  test('Should_deal_five_tokens_each_and_require_passing_when_four_are_playing', async ({ browser }) => {
    // Arrange
    // Four hands of five leaves two over, and the rules have each player pass one recorded token
    // across the table. Unlike the three-player deal, this one cannot settle itself.
    const host = await browser.newPage();
    const roomCode = await openTable(host, 4);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);

    // Act
    await deal(ada.page, null);
    await takeTokens([ada]);

    // Assert
    await expect(ada.page.getByText('Pass a token')).toBeVisible();
    expect(await ownHand(ada.page)).toHaveLength(5);
    for (let seat = 0; seat < 4; seat += 1) {
      expect(await statedHandSize(ada.page, seat)).toBe(5);
    }

    await leave([ada]);
  });

  test('Should_reach_play_when_the_pass_and_the_announcement_are_done', async ({ browser }) => {
    // Arrange
    const host = await browser.newPage();
    const roomCode = await openTable(host, 4);
    const ada = await sitDown(browser, roomCode, 'Ada', 0);
    await deal(ada.page, null);
    await takeTokens([ada]);

    // Act
    await ada.page.getByRole('button', { name: 'Done passing' }).click();
    await announceFewest(ada.page);

    // Assert
    // Hand sizes are unchanged by passing: everybody gives one and receives one, so the count on
    // every seat card is still five even though the contents moved.
    await expect(ada.page.getByText('InProgress')).toBeVisible();
    expect(await ownHand(ada.page)).toHaveLength(5);
    for (let seat = 0; seat < 4; seat += 1) {
      expect(await statedHandSize(ada.page, seat)).toBe(5);
    }

    await leave([ada]);
  });
});
