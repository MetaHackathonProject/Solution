import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

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

@Injectable({
  providedIn: 'root',
})
export class InterviewSessionsService {
  private readonly http = inject(HttpClient);

  create(request: CreateInterviewSessionRequest): Observable<CreateInterviewSessionResponse> {
    return this.http.post<CreateInterviewSessionResponse>(
      `${environment.apiUrl}/api/interview-sessions`,
      request,
    );
  }
}
