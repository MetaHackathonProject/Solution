import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';
import { statusLabel } from '../interview-session-status';
import { EmployerAssignment, InterviewTemplatesService } from '../interview-templates.service';

interface AssignmentGroup {
  templateId: string;
  templateName: string;
  jobTitle: string;
  company: string;
  items: EmployerAssignment[];
}

@Component({
  selector: 'app-employer-assignments',
  imports: [RouterLink, DatePipe],
  templateUrl: './employer-assignments.html',
  styleUrl: './employer-assignments.scss',
})
export class EmployerAssignments implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly templatesApi = inject(InterviewTemplatesService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly assignments = signal<EmployerAssignment[]>([]);
  protected readonly statusLabel = statusLabel;
  protected readonly groups = computed(() => this.groupByTemplate(this.assignments()));

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      this.loading.set(false);
      return;
    }

    this.templatesApi.listAssignments().subscribe({
      next: (assignments) => {
        this.assignments.set(assignments);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(
          err instanceof HttpErrorResponse
            ? err.error?.detail || err.message || 'Could not load assignments.'
            : 'Could not load assignments.',
        );
      },
    });
  }

  protected createdLabel(unixSeconds: number): Date {
    return new Date(unixSeconds * 1000);
  }

  protected candidateLabel(item: EmployerAssignment): string {
    return item.candidateEmail?.trim() || 'Candidate';
  }

  protected hasScore(item: EmployerAssignment): boolean {
    return item.status === 'COMPLETED' && item.score != null;
  }

  private groupByTemplate(assignments: EmployerAssignment[]): AssignmentGroup[] {
    const groups = new Map<string, AssignmentGroup>();
    for (const item of assignments) {
      const templateId = item.templateId || item.sessionId;
      const existing = groups.get(templateId);
      if (existing) {
        existing.items.push(item);
        continue;
      }

      groups.set(templateId, {
        templateId,
        templateName: item.templateName,
        jobTitle: item.jobTitle,
        company: item.company,
        items: [item],
      });
    }

    return [...groups.values()]
      .map((group) => ({
        ...group,
        items: [...group.items].sort((left, right) => this.compareCandidates(left, right)),
      }))
      .sort((left, right) => this.latestCreated(right.items) - this.latestCreated(left.items));
  }

  private compareCandidates(left: EmployerAssignment, right: EmployerAssignment): number {
    const leftScored = this.hasScore(left);
    const rightScored = this.hasScore(right);
    if (leftScored && rightScored) {
      const byScore = (right.score ?? 0) - (left.score ?? 0);
      return byScore !== 0 ? byScore : right.createdAt - left.createdAt;
    }

    if (leftScored !== rightScored) {
      return leftScored ? -1 : 1;
    }

    const leftDone = left.status === 'COMPLETED';
    const rightDone = right.status === 'COMPLETED';
    if (leftDone !== rightDone) {
      return leftDone ? -1 : 1;
    }

    return right.createdAt - left.createdAt;
  }

  private latestCreated(items: EmployerAssignment[]): number {
    return items.reduce((latest, item) => Math.max(latest, item.createdAt), 0);
  }
}
