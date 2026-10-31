import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAuth, OidcSecurityService } from 'angular-auth-oidc-client';
import { catchError, firstValueFrom, of, switchMap } from 'rxjs';
import { environment } from '../environments/environment';
import { AccountService } from './account.service';
import { routes } from './app.routes';
import { authInterceptor } from './auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAuth({ config: environment.cognito }),
    provideAppInitializer(() => {
      if (window.location.pathname.startsWith('/callback/oauth')) {
        return Promise.resolve();
      }

      const oidc = inject(OidcSecurityService);
      const account = inject(AccountService);
      return firstValueFrom(
        oidc.checkAuth(undefined, 'cognito').pipe(
          switchMap((result) =>
            result?.isAuthenticated
              ? account.load().pipe(catchError(() => of(null)))
              : of(null),
          ),
          catchError(() => of(null)),
        ),
      );
    }),
  ],
};
