import { Injectable, computed, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { environment } from '../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly oidc = inject(OidcSecurityService);

  readonly isAuthenticated = computed(() => this.oidc.authenticated().isAuthenticated);
  readonly email = computed(() => {
    const userData = this.oidc.userData().userData as { email?: string } | null;
    return userData?.email ?? '';
  });

  signIn(): void {
    this.oidc.authorize('cognito', {
      customParams: { prompt: 'login' },
    });
  }

  signOut(): void {
    this.oidc.getConfiguration('cognito').subscribe((config) => {
      if (!config?.clientId || !config.postLogoutRedirectUri) {
        return;
      }

      const url = new URL(environment.cognitoLogoutUrl);
      url.searchParams.set('client_id', config.clientId);
      url.searchParams.set('logout_uri', config.postLogoutRedirectUri);
      this.oidc.logoffLocal('cognito');
      window.location.assign(url.toString());
    });
  }
}
