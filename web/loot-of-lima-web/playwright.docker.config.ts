import { defineConfig } from '@playwright/test';
import base from './playwright.config';

/**
 * The same specs, against `docker compose up -d` instead of the dev servers.
 *
 * A config file rather than an environment variable in the npm script, because there is no way to
 * set one in a script that works in bash, PowerShell and cmd alike without adding a dependency to
 * do it. `E2E_BASE_URL` still works for anything else that wants to point the suite somewhere.
 *
 * This mode is the one that covers what the default mode cannot: the production bundle rather than
 * the dev server's, and nginx rather than the Angular proxy. The `^~` location precedence that made
 * the QR endpoint 404 was invisible to every other suite in this repository.
 */
export default defineConfig({
  ...base,

  // Nothing to start -- but dropping `webServer` also drops Playwright's readiness poll, and
  // `docker compose up -d` returns before Kestrel is listening. `globalSetup` puts the waiting back.
  webServer: undefined,
  globalSetup: './e2e/wait-for-server',

  use: { ...base.use, baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:4200' },
});
