import { defineConfig, devices } from '@playwright/test';

/**
 * The only suite that drives a browser.
 *
 * It runs in two modes against the same specs. By default it boots both halves itself -- `dotnet
 * run` and `ng serve` -- so `npm run e2e` needs nothing but the repository. Setting `E2E_BASE_URL`
 * points the same specs at an address that is already serving, which is how the compose stack gets
 * covered: `docker compose up -d` then `npm run e2e:docker` exercises the production bundle and the
 * nginx config rather than the dev server, and those are the parts no other suite touches at all.
 *
 * What belongs here is what a player sees. Arc arithmetic, the oracle and frame-level redaction are
 * asserted far more strictly by the .NET suites -- `Server.Tests` checks the raw bytes of every
 * frame -- and an e2e test that re-derives the engine is a slow second copy of a better suite.
 */
const external = process.env['E2E_BASE_URL'];
const baseURL = external ?? 'http://localhost:4200';

export default defineConfig({
  testDir: './e2e',

  // Every spec opens its own table through the UI, so they share nothing but the server. Room codes
  // are unique per game, which is what makes running them at once safe.
  fullyParallel: true,

  // Twice the default, and it buys nothing on a warm server: a spec here runs in one to three
  // seconds. What it covers is the first handful of specs on a *cold* one, which is the normal way
  // to run this — `docker compose up --build` then `e2e:docker`, or a `dotnet run` that has only
  // just been started for a swap. The readiness gate proves the server answers; it does not warm a
  // Debug build's JIT, and thirty specs arriving at once on a cold Kestrel push the first few past
  // thirty seconds. Those failures say nothing about the app, and a suite that fails on its own
  // documented sequence is worse than a slow one.
  timeout: 60_000,

  // A `.only` left behind passes locally and silently stops testing everything else in CI.
  forbidOnly: !!process.env['CI'],

  retries: process.env['CI'] ? 2 : 0,
  workers: process.env['CI'] ? 1 : undefined,
  reporter: process.env['CI'] ? [['github'], ['html', { open: 'never' }]] : [['list']],

  use: {
    baseURL,

    // On the first retry only: a trace of a passing run is a large file nobody opens.
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  // Skipped entirely when E2E_BASE_URL says something is already there.
  //
  // The API is booted first and waited for, because `ng serve` proxies to it: a client that comes
  // up first is not broken, but every spec would then race the server's first compile.
  webServer: external
    ? undefined
    : [
        {
          command: 'dotnet run --project ../../src/LootOfLima.Server',
          url: 'http://localhost:5000/api/scenarios',
          reuseExistingServer: !process.env['CI'],
          timeout: 180_000,
          stdout: 'pipe',
          stderr: 'pipe',
        },
        {
          command: 'npm start',
          url: 'http://localhost:4200',
          reuseExistingServer: !process.env['CI'],
          timeout: 180_000,
          stdout: 'pipe',
          stderr: 'pipe',
        },
      ],
});
