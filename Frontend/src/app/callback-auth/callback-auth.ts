import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { catchError, of } from 'rxjs';
import { AccountService } from '../account.service';

@Component({
  selector: 'app-callback-auth',
  templateUrl: './callback-auth.html',
  styles: `
    .auth-status {
      max-width: 1160px;
      margin: 0 auto;
      padding: 4.5rem 1.5rem;
      color: #5c6370;
      font-size: 0.95rem;
    }
  `,
})
export class CallbackAuth implements OnInit {
  private readonly router = inject(Router);
  private readonly oidc = inject(OidcSecurityService);
  private readonly account = inject(AccountService);

  ngOnInit(): void {
    this.oidc.checkAuth(undefined, 'cognito').subscribe((result) => {
      if (result.isAuthenticated) {
        const redirect = sessionStorage.getItem('post_login_redirect') || '/';
        sessionStorage.removeItem('post_login_redirect');
        this.account.load().pipe(catchError(() => of(null))).subscribe(() => {
          void this.router.navigateByUrl(redirect);
        });
      }
    });
  }
}
