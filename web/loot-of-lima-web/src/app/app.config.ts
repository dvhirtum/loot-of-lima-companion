import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch()),
    // Route parameters arrive as component inputs, so a page reads `roomCode` as a signal input
    // rather than subscribing to a router observable to learn which table it is showing.
    provideRouter(routes, withComponentInputBinding()),
  ],
};
