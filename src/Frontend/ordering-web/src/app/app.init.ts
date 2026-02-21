import { KeycloakService } from 'keycloak-angular';
import { environment } from '../environments/environment';

/**
 * Initialize Keycloak with Authorization Code + PKCE (no implicit flow).
 * Tokens are obtained via code exchange and kept in memory.
 */
export function initializeKeycloak(keycloak: KeycloakService) {
  return () =>
    keycloak
      .init({
        config: {
          url: environment.keycloak.url,
          realm: environment.keycloak.realm,
          clientId: environment.keycloak.clientId
        },
        initOptions: {
          onLoad: 'check-sso',
          flow: 'standard',
          pkceMethod: 'S256',
          responseMode: 'query',
          checkLoginIframe: false,
          // Must match the URL Keycloak redirects to (e.g. /orders?code=...) so token exchange succeeds
          redirectUri: typeof window !== 'undefined' ? window.location.origin + (window.location.pathname || '/') : 'http://localhost:4200/'
        },
        enableBearerInterceptor: true,
        bearerExcludedUrls: ['/assets', environment.keycloak.url],
        loadUserProfileAtStartUp: false
      })
      .catch((err: unknown) => {
        // Log full details: token exchange can succeed (200) but post-processing may throw without a proper Error
        const msg = err instanceof Error ? err.message : String(err ?? '(no error object)');
        const stack = err instanceof Error ? err.stack : undefined;
        console.error('Keycloak init failed', msg, stack ?? err);
        // If token was already set before the throw (e.g. URL cleanup failed), allow app to bootstrap
        try {
          if (keycloak.isLoggedIn()) {
            console.warn('Keycloak init reported failure but user is logged in; continuing bootstrap.');
            return Promise.resolve(true);
          }
        } catch {
          // ignore
        }
        return Promise.reject(err instanceof Error ? err : new Error(msg));
      });
}
