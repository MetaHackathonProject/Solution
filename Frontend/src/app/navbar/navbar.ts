import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';
import { BrandMark } from '../brand-mark/brand-mark';
import { ProfileMenu } from '../profile-menu/profile-menu';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, BrandMark, ProfileMenu],
  templateUrl: './navbar.html',
  styleUrl: './navbar.scss',
})
export class Navbar {
  protected readonly auth = inject(AuthService);
}
