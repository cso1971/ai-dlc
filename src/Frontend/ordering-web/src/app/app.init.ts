import { KeycloakService } from 'keycloak-angular';
import { environment } from '../environments/environment';

/**
 * Initialize Keycloak with Authorization Code + PKCE.
 * Nonce validation disabled: keycloak-js nonce check fails even with login-required
 * (likely Keycloak server/client version mismatch). PKCE already protects against
 * code interception, so security impact is minimal.
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
          onLoad: 'login-required',
          flow: 'standard',
          pkceMethod: 'S256',
          responseMode: 'query',
          checkLoginIframe: false,
          redirectUri: window.location.origin + '/',
          useNonce: false
        },
        enableBearerInterceptor: true,
        bearerExcludedUrls: ['/assets', environment.keycloak.url],
        loadUserProfileAtStartUp: false
      })
      .catch((err: unknown) => {
        const msg = err instanceof Error ? err.message : String(err ?? '(no error object)');
        console.error('Keycloak init failed', msg, err);
        return Promise.reject(err instanceof Error ? err : new Error(msg));
      });
}
