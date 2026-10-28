import {
  Component,
  ElementRef,
  HostListener,
  inject,
  signal,
} from '@angular/core';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-profile-menu',
  templateUrl: './profile-menu.html',
  styleUrl: './profile-menu.scss',
})
export class ProfileMenu {
  protected readonly auth = inject(AuthService);
  protected readonly open = signal(false);

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.open()) {
      return;
    }

    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.open.set(false);
  }

  protected toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.open.update((value) => !value);
  }

  protected signOut(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.open.set(false);
    this.auth.signOut();
  }
}
