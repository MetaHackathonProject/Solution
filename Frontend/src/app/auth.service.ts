import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { map } from 'rxjs';
import { environment } from '../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly oidc = inject(OidcSecurityService);

  readonly isAuthenticated = toSignal(this.oidc.isAuthenticated('cognito'), { initialValue: false });
  readonly email = toSignal(
    this.oidc.getUserData('cognito').pipe(map((user) => (user?.email as string | undefined) ?? '')),
    { initialValue: '' },
  );

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
