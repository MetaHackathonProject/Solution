import { Component, OnDestroy, afterRenderEffect, inject, input } from '@angular/core';
import { InterviewerTalkingHead } from './interviewer-talking-head';

@Component({
  selector: 'app-interviewer-avatar',
  templateUrl: './interviewer-avatar.html',
  styleUrl: './interviewer-avatar.scss',
})
export class InterviewerAvatar implements OnDestroy {
  readonly speaking = input(false);
  readonly live = input(false);
  readonly name = input('Interviewer');
  readonly status = input('Waiting');

  private readonly talkingHead = inject(InterviewerTalkingHead);
  protected readonly mouth = this.talkingHead.mouth;
  private fallbackTimer: number | null = null;

  constructor() {
    afterRenderEffect((onCleanup) => {
      if (!this.speaking()) {
        this.clearFallback();
        this.talkingHead.notifySpeechEnd();
        return;
      }

      this.fallbackTimer = window.setInterval(() => this.talkingHead.speakFallback(), 90) as unknown as number;
      onCleanup(() => this.clearFallback());
    });
  }

  ngOnDestroy(): void {
    this.clearFallback();
    this.talkingHead.notifySpeechEnd();
  }

  private clearFallback(): void {
    if (this.fallbackTimer) {
      clearInterval(this.fallbackTimer);
      this.fallbackTimer = null;
    }
  }
}
