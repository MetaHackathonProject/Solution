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

export type OverviewStatus = 'NOT_AVAILABLE' | 'GENERATING' | 'READY' | 'ERROR' | string;

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
  overviewStatus?: OverviewStatus;
}

export interface InterviewOverviewQuestion {
  question: string;
  answer: string;
  opinion: string;
  recommendations: string[];
}

export interface InterviewOverviewInteractiveTask {
  taskId: string;
  kind: string;
  score: number;
  strengths: string[];
  gaps: string[];
  summary: string;
}

export interface InterviewOverview {
  summary: string;
  score: number;
  questions: InterviewOverviewQuestion[];
  recommendations: string[];
  interactiveTasks: InterviewOverviewInteractiveTask[];
}

export interface QuestionHelper {
  questionId: string;
  module: string;
  question: string;
  suggestedAnswer: string;
  hints: string[];
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

  getOverview(sessionId: string): Observable<InterviewOverview> {
    return this.http.get<InterviewOverview>(`${this.baseUrl}/${sessionId}/overview`);
  }

  getActiveQuestionHelpers(sessionId: string): Observable<QuestionHelper[]> {
    return this.http.get<QuestionHelper[]>(`${this.baseUrl}/${sessionId}/question-helpers/active`);
  }

  getSessionQuestionHelpers(sessionId: string): Observable<QuestionHelper[]> {
    return this.http.get<QuestionHelper[]>(`${this.baseUrl}/${sessionId}/question-helpers`);
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
