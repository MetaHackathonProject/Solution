import { HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, switchMap } from 'rxjs';

export function authInterceptor(request: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
  return inject(OidcSecurityService).getAccessToken('cognito').pipe(
    switchMap((token) => {
      if (!token) {
        return next(request);
      }

      return next(request.clone({
        setHeaders: { Authorization: `Bearer ${token}` },
      }));
    }),
  );
}
