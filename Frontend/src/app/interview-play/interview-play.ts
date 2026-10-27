import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  Conversation,
  Mode,
  Status,
  TextConversation,
  VoiceConversation,
} from '@elevenlabs/client';
import { forkJoin } from 'rxjs';
import { AuthService } from '../auth.service';
import { toElevenLabsLanguage } from '../eleven-labs-language';
import {
  VoiceAgentContextResponse,
  VoiceAgentService,
} from '../voice-agent.service';

@Component({
  selector: 'app-interview-play',
  imports: [RouterLink],
  templateUrl: './interview-play.html',
  styleUrl: './interview-play.scss',
})
export class InterviewPlay implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly voiceAgent = inject(VoiceAgentService);

  protected readonly loading = signal(true);
  protected readonly starting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly connectionStatus = signal<Status | 'idle'>('idle');
  protected readonly mode = signal<Mode>('listening');

  protected sessionId = '';
  private signedUrl: string | null = null;
  private conversationId: string | null = null;
  private agentContext: VoiceAgentContextResponse | null = null;
  private conversation: VoiceConversation | TextConversation | null = null;
  private ending = false;

  ngOnInit(): void {
    this.sessionId = this.route.snapshot.paramMap.get('sessionId') ?? '';
    if (!this.sessionId) {
      this.loading.set(false);
      this.error.set('Missing session id.');
      return;
    }

    if (!this.auth.isAuthenticated()) {
      this.loading.set(false);
      return;
    }

    forkJoin({
      signed: this.voiceAgent.getSignedUrl(this.sessionId),
      context: this.voiceAgent.getVoiceContext(this.sessionId),
    }).subscribe({
      next: ({ signed, context }) => {
        this.signedUrl = signed.signedUrl;
        this.conversationId = signed.conversationId;
        this.agentContext = context;
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(this.describeError(err));
      },
    });
  }

  ngOnDestroy(): void {
    void this.endConversationLocally();
  }

  protected async startConversation(): Promise<void> {
    if (!this.sessionId || !this.signedUrl || this.starting()) {
      return;
    }

    this.starting.set(true);
    this.error.set(null);

    try {
      await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      this.starting.set(false);
      this.error.set('Microphone access is required to start the interview.');
      return;
    }

    const dynamicVariables: Record<string, string> = {
      sessionId: this.sessionId,
    };

    if (this.agentContext?.jobTitle) {
      dynamicVariables['job_title'] = this.agentContext.jobTitle;
    }
    if (this.agentContext?.company) {
      dynamicVariables['company'] = this.agentContext.company;
    }
    if (this.agentContext?.jobOverview) {
      dynamicVariables['job_overview'] = this.agentContext.jobOverview;
    }

    const language = toElevenLabsLanguage(this.agentContext?.language);

    try {
      this.conversation = await Conversation.startSession({
        signedUrl: this.signedUrl,
        dynamicVariables,
        overrides: language
          ? {
              agent: { language },
            }
          : undefined,
        onStatusChange: ({ status }) => {
          this.connectionStatus.set(status);
        },
        onModeChange: ({ mode }) => {
          this.mode.set(mode);
        },
        onError: () => {
          this.error.set('The voice interview connection failed. Try again.');
          this.starting.set(false);
        },
        onConnect: () => {
          this.starting.set(false);
        },
        onDisconnect: () => {
          this.connectionStatus.set('disconnected');
          this.completeAndLeave();
        },
      });
    } catch {
      this.starting.set(false);
      this.error.set('Could not start the voice interview. Try again.');
    }
  }

  protected async stopConversation(): Promise<void> {
    if (!this.conversation) {
      return;
    }

    await this.conversation.endSession();
  }

  private async endConversationLocally(): Promise<void> {
    if (!this.conversation) {
      return;
    }

    const conversation = this.conversation;
    this.conversation = null;
    await conversation.endSession();
  }

  private completeAndLeave(): void {
    if (this.ending) {
      return;
    }

    this.ending = true;
    const conversationId = this.conversation?.getId() ?? this.conversationId;
    this.conversation = null;

    if (!conversationId) {
      void this.router.navigate(['/interviews', this.sessionId]);
      return;
    }

    this.voiceAgent.endConversation(this.sessionId, conversationId).subscribe({
      next: () => {
        void this.router.navigate(['/interviews', this.sessionId]);
      },
      error: () => {
        void this.router.navigate(['/interviews', this.sessionId]);
      },
    });
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 400) {
        return err.error?.detail || 'This session is not ready for a live interview.';
      }

      if (err.status === 404) {
        return 'Interview session not found.';
      }

      if (err.status === 401) {
        return 'Sign in expired. Sign in again and retry.';
      }

      if (err.status === 0) {
        return 'Could not reach the API. Check your connection and try again.';
      }

      return err.error?.title || err.error?.detail || err.message || 'Could not prepare the interview.';
    }

    return 'Could not prepare the interview.';
  }
}
