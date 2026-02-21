/** All APIs via Gateway (YARP) - configure base URL and Keycloak for production */
export const environment = {
  production: true,
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
