/** All APIs via Gateway (YARP) - single entry point at :5000 */
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  customersApiUrl: 'http://localhost:5000',
  aiApiUrl: 'http://localhost:5000',
  orchestratorApiUrl: 'http://localhost:5000',
  /** Keycloak: use realm "playground" (must match Keycloak server realm name). */
  keycloak: {
    url: 'http://localhost:8180/',
    realm: 'playground',
    clientId: 'ordering-web'
  }
};
