import { Component, inject } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AccountService } from './account.service';
import { AuthService } from './auth.service';
import { Navbar } from './navbar/navbar';

@Component({
  selector: 'app-root',
  imports: [Navbar, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  constructor() {
    const auth = inject(AuthService);
    const account = inject(AccountService);
    const router = inject(Router);

    router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe((event) => {
      const url = event.urlAfterRedirects.split('?')[0];
      if (!auth.isAuthenticated() || !account.loaded()) {
        return;
      }

      if (account.role()) {
        if (url === '/welcome') {
          void router.navigateByUrl(account.isEmployer() ? '/templates' : '/');
        }

        return;
      }

      if (url === '/welcome' || url.startsWith('/join/') || url.startsWith('/callback/')) {
        return;
      }

      void router.navigateByUrl('/welcome');
    });
  }
}
