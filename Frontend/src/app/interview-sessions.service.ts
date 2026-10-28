import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { InterviewSessionStatus } from './interview-session-status';

export interface CreateInterviewSessionRequest {
  name: string;
  offerUrl: string;
  settings: {
    recruiterLanguage: string;
    difficulty: 'auto' | 'easy' | 'medium' | 'hard';
  };
}

export interface CreateInterviewSessionResponse {
  sessionId: string;
}

export interface InterviewSession {
  sessionId: string;
  name: string;
  offerUrl: string | null;
  status: InterviewSessionStatus | string;
  createdAt: number;
  updatedAt: number;
  settings: {
    recruiterLanguage: string;
    difficulty: string;
  };
}

@Injectable({
  providedIn: 'root',
})
export class InterviewSessionsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/interview-sessions`;

  create(request: CreateInterviewSessionRequest): Observable<CreateInterviewSessionResponse> {
    return this.http.post<CreateInterviewSessionResponse>(this.baseUrl, request);
  }

  get(sessionId: string): Observable<InterviewSession> {
    return this.http.get<InterviewSession>(`${this.baseUrl}/${sessionId}`);
  }

  list(): Observable<InterviewSession[]> {
    return this.http.get<InterviewSession[]>(this.baseUrl);
  }

  delete(sessionId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${sessionId}`);
  }

  retake(sessionId: string): Observable<{ sessionId: string; questionsSeeded: number }> {
    return this.http.post<{ sessionId: string; questionsSeeded: number }>(
      `${this.baseUrl}/${sessionId}/retake`,
      {},
    );
  }
}
