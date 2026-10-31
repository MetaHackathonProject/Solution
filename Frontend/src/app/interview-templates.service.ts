import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { OverviewStatus } from './interview-sessions.service';

export const INTERVIEW_MODULES = [
  'Communication',
  'TechnicalKnowledge',
  'PracticalSkills',
  'ArchitectureAndDesign',
  'ProblemSolving',
  'ExperienceValidation',
  'DomainKnowledge',
  'Behavioral',
  'ForeignLanguage',
] as const;

export const EXPECTED_INPUTS = ['code', 'diagram', 'schema', 'text', 'free_string'] as const;

export interface TemplateSpokenQuestion {
  id?: string | null;
  module: string;
  question: string;
  assessmentGoal: string;
}

export interface TemplateInteractiveTask {
  id?: string | null;
  module: string;
  questionContent: string;
  questionContentFormatted?: string | null;
  expectedInput: string;
  assessmentGoal: string;
}

export interface SaveInterviewTemplateRequest {
  name: string;
  jobTitle: string;
  company: string;
  language: string;
  offerUrl?: string | null;
  spokenQuestions: TemplateSpokenQuestion[];
  interactiveTasks: TemplateInteractiveTask[];
}

export interface InterviewTemplate extends SaveInterviewTemplateRequest {
  templateId: string;
  inviteCode: string;
  createdAt: number;
  updatedAt: number;
}

export interface InterviewInvitePreview {
  inviteCode: string;
  name: string;
  jobTitle: string;
  company: string;
  language: string;
}

export interface JoinInterviewTemplateResponse {
  sessionId: string;
  alreadyJoined: boolean;
}

export interface EmployerAssignment {
  sessionId: string;
  templateId: string;
  templateName: string;
  jobTitle: string;
  company: string;
  candidateUserId: string;
  status: string;
  overviewStatus: OverviewStatus;
  createdAt: number;
}

export interface ImportedOffer {
  title: string | null;
  company: string | null;
  offerUrl: string;
}

@Injectable({
  providedIn: 'root',
})
export class InterviewTemplatesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/interview-templates`;

  list(): Observable<InterviewTemplate[]> {
    return this.http.get<InterviewTemplate[]>(this.baseUrl);
  }

  get(templateId: string): Observable<InterviewTemplate> {
    return this.http.get<InterviewTemplate>(`${this.baseUrl}/${templateId}`);
  }

  create(request: SaveInterviewTemplateRequest): Observable<InterviewTemplate> {
    return this.http.post<InterviewTemplate>(this.baseUrl, request);
  }

  update(templateId: string, request: SaveInterviewTemplateRequest): Observable<InterviewTemplate> {
    return this.http.put<InterviewTemplate>(`${this.baseUrl}/${templateId}`, request);
  }

  delete(templateId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${templateId}`);
  }

  importOffer(offerUrl: string): Observable<ImportedOffer> {
    return this.http.post<ImportedOffer>(`${this.baseUrl}/import-offer`, { offerUrl });
  }

  previewInvite(inviteCode: string): Observable<InterviewInvitePreview> {
    return this.http.get<InterviewInvitePreview>(`${this.baseUrl}/join/${inviteCode}`);
  }

  join(inviteCode: string): Observable<JoinInterviewTemplateResponse> {
    return this.http.post<JoinInterviewTemplateResponse>(`${this.baseUrl}/join/${inviteCode}`, {});
  }

  listAssignments(): Observable<EmployerAssignment[]> {
    return this.http.get<EmployerAssignment[]>(`${this.baseUrl}/assignments`);
  }
}
