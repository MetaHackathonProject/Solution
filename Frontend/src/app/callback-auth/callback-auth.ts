import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';

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

  ngOnInit(): void {
    this.oidc.checkAuth(undefined, 'cognito').subscribe((result) => {
      if (result.isAuthenticated) {
        void this.router.navigateByUrl('/');
      }
    });
  }
}
