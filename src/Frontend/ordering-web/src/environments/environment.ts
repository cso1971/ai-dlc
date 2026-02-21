/** All APIs via Gateway (YARP) - single entry point at :5000 */
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000',
  customersApiUrl: 'http://localhost:5000',
  aiApiUrl: 'http://localhost:5000',
  orchestratorApiUrl: 'http://localhost:5000',
  keycloak: {
    url: 'http://localhost:8180/',
    realm: 'playground',
    clientId: 'ordering-web'
  }
};
