import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { environment } from '../environments/environment';

export interface InteractiveTaskResponse {
  taskId: string;
  module: string;
  questionContentFormatted: string;
  questionContent: string;
  status: string;
  expectedInput: string;
}

export interface SubmitInteractiveAnswerResponse {
  visionReviewStatus: string;
}

export interface VisionReviewResponse {
  taskId: string;
  status: string;
  description: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class InteractiveTaskService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/interview-sessions`;

  getActive(sessionId: string): Observable<InteractiveTaskResponse | null> {
    return this.http
      .get<InteractiveTaskResponse>(`${this.baseUrl}/${sessionId}/interactive-task`, {
        observe: 'response',
      })
      .pipe(
        map((response) => (response.status === 204 ? null : response.body)),
        catchError((err: HttpErrorResponse) => {
          if (err.status === 404 || err.status === 204) {
            return of(null);
          }
          return throwError(() => err);
        }),
      );
  }

  getById(sessionId: string, taskId: string): Observable<InteractiveTaskResponse> {
    return this.http.get<InteractiveTaskResponse>(
      `${this.baseUrl}/${sessionId}/interactive-task/${taskId}`,
    );
  }

  getVisionReview(sessionId: string, taskId: string): Observable<VisionReviewResponse> {
    return this.http.get<VisionReviewResponse>(
      `${this.baseUrl}/${sessionId}/interactive-task/${taskId}/vision-review`,
    );
  }

  submitText(
    sessionId: string,
    taskId: string,
    text: string,
  ): Observable<SubmitInteractiveAnswerResponse> {
    const body = new FormData();
    body.append('text', text);
    return this.http.post<SubmitInteractiveAnswerResponse>(
      `${this.baseUrl}/${sessionId}/interactive-task/${taskId}/answer`,
      body,
    );
  }

  submitFile(
    sessionId: string,
    taskId: string,
    file: File,
  ): Observable<SubmitInteractiveAnswerResponse> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<SubmitInteractiveAnswerResponse>(
      `${this.baseUrl}/${sessionId}/interactive-task/${taskId}/answer`,
      body,
    );
  }
}
