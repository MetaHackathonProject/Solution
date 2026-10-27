import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface InteractiveTaskResponse {
  taskId: string;
  module: string;
  questionContentFormatted: string;
  questionContent: string;
  status: string;
  expectedInput: string;
}

@Injectable({
  providedIn: 'root',
})
export class InteractiveTaskService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/interview-sessions`;

  getActive(sessionId: string): Observable<InteractiveTaskResponse> {
    return this.http.get<InteractiveTaskResponse>(
      `${this.baseUrl}/${sessionId}/interactive-task`,
    );
  }

  getById(sessionId: string, taskId: string): Observable<InteractiveTaskResponse> {
    return this.http.get<InteractiveTaskResponse>(
      `${this.baseUrl}/${sessionId}/interactive-task/${taskId}`,
    );
  }

  submitText(sessionId: string, taskId: string, text: string): Observable<void> {
    const body = new FormData();
    body.append('text', text);
    return this.http.post<void>(
      `${this.baseUrl}/${sessionId}/interactive-task/${taskId}/answer`,
      body,
    );
  }

  submitFile(sessionId: string, taskId: string, file: File): Observable<void> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<void>(
      `${this.baseUrl}/${sessionId}/interactive-task/${taskId}/answer`,
      body,
    );
  }
}
