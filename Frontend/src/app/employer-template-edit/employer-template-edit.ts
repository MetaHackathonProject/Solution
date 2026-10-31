import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  EXPECTED_INPUTS,
  INTERVIEW_MODULES,
  InterviewTemplate,
  InterviewTemplatesService,
  TemplateInteractiveTask,
  TemplateSpokenQuestion,
} from '../interview-templates.service';

@Component({
  selector: 'app-employer-template-edit',
  imports: [FormsModule, RouterLink],
  templateUrl: './employer-template-edit.html',
  styleUrl: './employer-template-edit.scss',
})
export class EmployerTemplateEdit implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly templatesApi = inject(InterviewTemplatesService);

  protected readonly modules = INTERVIEW_MODULES;
  protected readonly expectedInputs = EXPECTED_INPUTS;
  protected readonly languages = [
    'English',
    'Polish',
    'German',
    'French',
    'Spanish',
    'Italian',
    'Ukrainian',
    'Dutch',
    'Portuguese',
    'Czech',
    'Swedish',
    'Japanese',
  ];

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly importing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly templateId = signal<string | null>(null);
  protected readonly inviteCode = signal('');
  protected readonly offerUrl = signal('');

  protected name = '';
  protected jobTitle = '';
  protected company = '';
  protected language = 'English';
  protected spoken: TemplateSpokenQuestion[] = [this.emptySpoken()];
  protected tasks: TemplateInteractiveTask[] = [];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('templateId');
    if (!id || id === 'new') {
      this.loading.set(false);
      return;
    }

    this.templateId.set(id);
    this.templatesApi.get(id).subscribe({
      next: (template) => this.apply(template),
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(this.describeError(err, 'Could not load this template.'));
      },
    });
  }

  protected addSpoken(): void {
    this.spoken = [...this.spoken, this.emptySpoken()];
  }

  protected removeSpoken(index: number): void {
    this.spoken = this.spoken.filter((_, i) => i !== index);
    if (this.spoken.length === 0) {
      this.spoken = [this.emptySpoken()];
    }
  }

  protected addTask(): void {
    this.tasks = [...this.tasks, this.emptyTask()];
  }

  protected removeTask(index: number): void {
    this.tasks = this.tasks.filter((_, i) => i !== index);
  }

  protected importOffer(): void {
    const url = this.normalizeUrl(this.offerUrl().trim());
    if (!url || this.importing()) {
      return;
    }

    this.importing.set(true);
    this.error.set(null);
    this.templatesApi.importOffer(url).subscribe({
      next: (imported) => {
        this.offerUrl.set(imported.offerUrl);
        if (imported.title && !this.jobTitle.trim()) {
          this.jobTitle = imported.title;
        }
        if (imported.company && !this.company.trim()) {
          this.company = imported.company;
        }
        this.importing.set(false);
      },
      error: (err: unknown) => {
        this.importing.set(false);
        this.error.set(this.describeError(err, 'Could not read that posting.'));
      },
    });
  }

  protected save(): void {
    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const request = {
      name: this.name.trim(),
      jobTitle: this.jobTitle.trim(),
      company: this.company.trim(),
      language: this.language,
      offerUrl: this.normalizeUrl(this.offerUrl().trim()) || null,
      spokenQuestions: this.spoken.map((question) => ({
        ...question,
        question: question.question.trim(),
        assessmentGoal: question.assessmentGoal.trim(),
      })),
      interactiveTasks: this.tasks.map((task) => ({
        ...task,
        questionContent: task.questionContent.trim(),
        assessmentGoal: task.assessmentGoal.trim(),
      })),
    };

    const id = this.templateId();
    const save$ = id
      ? this.templatesApi.update(id, request)
      : this.templatesApi.create(request);

    save$.subscribe({
      next: (template) => {
        this.apply(template);
        this.saving.set(false);
        if (!id) {
          void this.router.navigate(['/templates', template.templateId], { replaceUrl: true });
        }
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(this.describeError(err, 'Could not save this template.'));
      },
    });
  }

  protected inviteUrl(): string {
    return this.inviteCode() ? `${window.location.origin}/join/${this.inviteCode()}` : '';
  }

  protected async copyLink(): Promise<void> {
    const url = this.inviteUrl();
    if (!url) {
      return;
    }

    try {
      await navigator.clipboard.writeText(url);
      this.copied.set(true);
    } catch {
      this.error.set('Could not copy the invite link.');
    }
  }

  private apply(template: InterviewTemplate): void {
    this.templateId.set(template.templateId);
    this.inviteCode.set(template.inviteCode);
    this.name = template.name;
    this.jobTitle = template.jobTitle;
    this.company = template.company;
    this.language = template.language;
    this.offerUrl.set(template.offerUrl ?? '');
    this.spoken = template.spokenQuestions.length
      ? template.spokenQuestions.map((question) => ({ ...question }))
      : [this.emptySpoken()];
    this.tasks = template.interactiveTasks.map((task) => ({ ...task }));
    this.copied.set(false);
    this.loading.set(false);
  }

  private emptySpoken(): TemplateSpokenQuestion {
    return {
      module: 'Communication',
      question: '',
      assessmentGoal: '',
    };
  }

  private emptyTask(): TemplateInteractiveTask {
    return {
      module: 'PracticalSkills',
      questionContent: '',
      expectedInput: 'code',
      assessmentGoal: '',
    };
  }

  private normalizeUrl(value: string): string {
    if (!value || /^https?:\/\//i.test(value)) {
      return value;
    }

    return `https://${value}`;
  }

  private describeError(err: unknown, fallback: string): string {
    if (err instanceof HttpErrorResponse) {
      return err.error?.detail || err.message || fallback;
    }

    return fallback;
  }
}
