import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export type VoiceAgentPurpose = 'interview' | 'walkthrough';

export interface VoiceAgentSignedUrlResponse {
  signedUrl: string;
  conversationId: string;
  isResume: boolean;
}

export interface VoiceAgentContextResponse {
  jobTitle: string | null;
  company: string | null;
  jobOverview: string | null;
  language: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class VoiceAgentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api`;

  getSignedUrl(
    sessionId: string,
    purpose: VoiceAgentPurpose = 'interview',
  ): Observable<VoiceAgentSignedUrlResponse> {
    const params = new HttpParams().set('sessionId', sessionId).set('purpose', purpose);
    return this.http.get<VoiceAgentSignedUrlResponse>(`${this.baseUrl}/voice-agent/signed-url`, {
      params,
    });
  }

  getVoiceContext(sessionId: string): Observable<VoiceAgentContextResponse> {
    return this.http.get<VoiceAgentContextResponse>(
      `${this.baseUrl}/interview-sessions/${sessionId}/voice-context`,
    );
  }

  endConversation(sessionId: string, conversationId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/voice-agent/end-conversation`, {
      sessionId,
      conversationId,
    });
  }
}
