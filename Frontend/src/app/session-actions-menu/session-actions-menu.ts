import {
  Component,
  ElementRef,
  HostListener,
  input,
  output,
  signal,
} from '@angular/core';

@Component({
  selector: 'app-session-actions-menu',
  templateUrl: './session-actions-menu.html',
  styleUrl: './session-actions-menu.scss',
})
export class SessionActionsMenu {
  readonly disabled = input(false);
  readonly busy = input(false);
  readonly label = input('Session actions');
  readonly delete = output<MouseEvent>();

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

    if (this.disabled() || this.busy()) {
      return;
    }

    this.open.update((value) => !value);
  }

  protected onDelete(event: MouseEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.open.set(false);
    this.delete.emit(event);
  }
}
