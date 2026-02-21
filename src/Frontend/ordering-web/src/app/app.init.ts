import { KeycloakService } from 'keycloak-angular';
import { environment } from '../environments/environment';

/**
 * Initialize Keycloak with Authorization Code + PKCE (no implicit flow).
 * Tokens are obtained via code exchange and kept in memory.
 */
export function initializeKeycloak(keycloak: KeycloakService) {
  return () =>
    keycloak.init({
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
        redirectUri: typeof window !== 'undefined' ? window.location.origin + '/' : 'http://localhost:4200/'
      },
      enableBearerInterceptor: true,
      bearerExcludedUrls: ['/assets', environment.keycloak.url],
      loadUserProfileAtStartUp: false
    });
}
