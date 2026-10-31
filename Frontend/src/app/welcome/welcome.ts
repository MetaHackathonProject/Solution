import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AccountService, UserRole } from '../account.service';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-welcome',
  templateUrl: './welcome.html',
  styleUrl: './welcome.scss',
})
export class Welcome {
  protected readonly auth = inject(AuthService);
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);

  protected readonly submitting = signal<UserRole | null>(null);
  protected readonly error = signal<string | null>(null);

  protected choose(role: UserRole): void {
    if (this.submitting()) {
      return;
    }

    this.submitting.set(role);
    this.error.set(null);
    this.account.setRole(role).subscribe({
      next: () => {
        void this.router.navigateByUrl(role === 'Employer' ? '/templates' : '/');
      },
      error: (err: unknown) => {
        this.submitting.set(null);
        this.error.set(this.describeError(err));
      },
    });
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 409) {
        return err.error?.detail || 'A role is already set on this account.';
      }

      return err.error?.detail || err.message || 'Could not save your role.';
    }

    return 'Could not save your role.';
  }
}
