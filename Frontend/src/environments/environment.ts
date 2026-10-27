import { env } from './env.values';

const origin = globalThis.location?.origin ?? 'http://localhost:4200';

export const environment = {
  apiUrl: 'https://api.hackathon-solution.com',
  cognito: {
    authority: env.COGNITO_AUTHORITY,
    clientId: env.COGNITO_CLIENT_ID,
    redirectUrl: `${origin}/callback/oauth`,
    postLogoutRedirectUri: origin,
    scope: 'openid email',
    responseType: 'code',
    configId: 'cognito',
    useRefreshToken: true,
  },
  cognitoLogoutUrl: 'https://auth.hackathon-solution.com/logout',
};
