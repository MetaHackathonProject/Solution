import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';
import { BrandMark } from '../brand-mark/brand-mark';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, BrandMark],
  templateUrl: './navbar.html',
  styleUrl: './navbar.scss',
})
export class Navbar {
  protected readonly auth = inject(AuthService);
}
