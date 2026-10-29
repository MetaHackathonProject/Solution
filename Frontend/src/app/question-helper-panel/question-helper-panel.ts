import { Component, input, signal } from '@angular/core';
import { QuestionHelper } from '../interview-sessions.service';

@Component({
  selector: 'app-question-helper-panel',
  templateUrl: './question-helper-panel.html',
  styleUrl: './question-helper-panel.scss',
})
export class QuestionHelperPanel {
  readonly helpers = input<QuestionHelper[]>([]);
  readonly title = input('Suggested answers');

  protected readonly expandedIds = signal<ReadonlySet<string>>(new Set());

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
}
