import { Component, OnDestroy, effect, input, signal } from '@angular/core';
import { QuestionHelper } from '../interview-sessions.service';

@Component({
  selector: 'app-question-helper-panel',
  templateUrl: './question-helper-panel.html',
  styleUrl: './question-helper-panel.scss',
})
export class QuestionHelperPanel implements OnDestroy {
  readonly helpers = input<QuestionHelper[]>([]);
  readonly title = input('Suggested answers');
  /** Live interview: wait this long after a new question before showing the hint. */
  readonly revealDelayMs = input(0);

  protected readonly visibleHelpers = signal<QuestionHelper[]>([]);
  protected readonly expandedIds = signal<ReadonlySet<string>>(new Set());

  private revealTimer: ReturnType<typeof setTimeout> | null = null;
  private lastQuestionKey = '';
  private revealedKey = '';

  constructor() {
    effect(() => {
      const helpers = this.helpers().slice(0, 1);
      const delay = this.revealDelayMs();
      const key = helpers.map((helper) => helper.questionId).join(',');

      if (!helpers.length) {
        this.clearRevealTimer();
        this.lastQuestionKey = '';
        this.revealedKey = '';
        this.visibleHelpers.set([]);
        this.expandedIds.set(new Set());
        return;
      }

      if (delay <= 0) {
        this.clearRevealTimer();
        this.lastQuestionKey = key;
        this.revealedKey = key;
        this.visibleHelpers.set(helpers);
        return;
      }

      if (key === this.revealedKey) {
        this.visibleHelpers.set(helpers);
        return;
      }

      if (key === this.lastQuestionKey && this.revealTimer) {
        return;
      }

      this.lastQuestionKey = key;
      this.visibleHelpers.set([]);
      this.expandedIds.set(new Set());
      this.clearRevealTimer();
      this.revealTimer = setTimeout(() => {
        this.revealTimer = null;
        this.revealedKey = key;
        this.visibleHelpers.set(helpers);
      }, delay);
    });
  }

  ngOnDestroy(): void {
    this.clearRevealTimer();
  }

  protected isExpanded(questionId: string): boolean {
    return this.expandedIds().has(questionId);
  }

  protected toggle(questionId: string): void {
    const next = new Set(this.expandedIds());
    if (next.has(questionId)) {
      next.delete(questionId);
    } else {
      next.add(questionId);
    }

    this.expandedIds.set(next);
  }

  private clearRevealTimer(): void {
    if (this.revealTimer) {
      clearTimeout(this.revealTimer);
      this.revealTimer = null;
    }
  }
}
