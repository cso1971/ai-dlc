import { KeycloakService } from 'keycloak-angular';
import { environment } from '../environments/environment';

/**
 * Initialize Keycloak with Authorization Code + PKCE.
 * Uses 'login-required' to avoid the nonce mismatch caused by 'check-sso'
 * silent iframe, which overwrites the stored nonce before the main code exchange.
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
          redirectUri: window.location.origin + '/'
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
