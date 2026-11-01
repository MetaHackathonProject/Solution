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

export const MODULE_OPTIONS = [
  {
    id: 'Communication',
    title: 'Communication',
    description: 'How clearly they explain ideas and work with others.',
  },
  {
    id: 'TechnicalKnowledge',
    title: 'Technical knowledge',
    description: 'Concepts, tools, and how the stack actually works.',
  },
  {
    id: 'PracticalSkills',
    title: 'Practical skills',
    description: 'Hands-on work they would do on the job.',
  },
  {
    id: 'ArchitectureAndDesign',
    title: 'Architecture and design',
    description: 'How they structure systems and trade off choices.',
  },
  {
    id: 'ProblemSolving',
    title: 'Problem solving',
    description: 'How they break down a hard problem and decide next steps.',
  },
  {
    id: 'ExperienceValidation',
    title: 'Experience',
    description: 'What they have actually shipped, owned, or led.',
  },
  {
    id: 'DomainKnowledge',
    title: 'Domain knowledge',
    description: 'Industry, product, or business context for this role.',
  },
  {
    id: 'Behavioral',
    title: 'Behavioral',
    description: 'How they handle conflict, feedback, and teamwork.',
  },
  {
    id: 'ForeignLanguage',
    title: 'Foreign language',
    description: 'Speaking the language this role needs, if it is not the session language.',
  },
] as const satisfies ReadonlyArray<{
  id: (typeof INTERVIEW_MODULES)[number];
  title: string;
  description: string;
}>;

export const MODULE_LABELS: Record<(typeof INTERVIEW_MODULES)[number], string> = {
  Communication: 'Communication',
  TechnicalKnowledge: 'Technical knowledge',
  PracticalSkills: 'Practical skills',
  ArchitectureAndDesign: 'Architecture and design',
  ProblemSolving: 'Problem solving',
  ExperienceValidation: 'Experience',
  DomainKnowledge: 'Domain knowledge',
  Behavioral: 'Behavioral',
  ForeignLanguage: 'Foreign language',
};

export const EXPECTED_INPUTS = ['code', 'diagram', 'schema', 'text', 'free_string'] as const;

export const EXPECTED_INPUT_OPTIONS = [
  {
    id: 'code',
    title: 'Code editor',
    description: 'Write or complete a short program in the browser.',
  },
  {
    id: 'diagram',
    title: 'Diagram',
    description: 'Sketch a flow, architecture, or sequence.',
  },
  {
    id: 'schema',
    title: 'Data model',
    description: 'Draw tables, entities, or relationships.',
  },
  {
    id: 'text',
    title: 'Written answer',
    description: 'A longer written explanation or plan.',
  },
  {
    id: 'free_string',
    title: 'Short text',
    description: 'A brief written reply or list.',
  },
] as const satisfies ReadonlyArray<{
  id: (typeof EXPECTED_INPUTS)[number];
  title: string;
  description: string;
}>;

export const EXPECTED_INPUT_LABELS: Record<(typeof EXPECTED_INPUTS)[number], string> = {
  code: 'Code editor',
  diagram: 'Diagram',
  schema: 'Data model',
  text: 'Written answer',
  free_string: 'Short text',
};

export function moduleLabel(module: string): string {
  return MODULE_LABELS[module as (typeof INTERVIEW_MODULES)[number]] ?? module;
}

export function moduleDescription(module: string): string {
  return MODULE_OPTIONS.find((option) => option.id === module)?.description ?? '';
}

export function expectedInputLabel(input: string): string {
  return EXPECTED_INPUT_LABELS[input as (typeof EXPECTED_INPUTS)[number]] ?? input;
}

export function expectedInputDescription(input: string): string {
  return EXPECTED_INPUT_OPTIONS.find((option) => option.id === input)?.description ?? '';
}

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
  candidateEmail?: string;
  status: string;
  overviewStatus: OverviewStatus;
  createdAt: number;
  score?: number | null;
}

export interface ImportedOffer {
  title: string | null;
  company: string | null;
  offerUrl: string;
}

export interface GeneratedTemplateFromOffer {
  sessionId: string;
  status: string;
  title: string | null;
  company: string | null;
  offerUrl: string;
  spokenQuestions: TemplateSpokenQuestion[];
  interactiveTasks: TemplateInteractiveTask[];
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

  startOfferGeneration(
    offerUrl: string,
    language: string,
  ): Observable<{ sessionId: string }> {
    return this.http.post<{ sessionId: string }>(`${this.baseUrl}/generate-from-offer`, {
      offerUrl,
      language,
    });
  }

  getGeneratedFromOffer(sessionId: string): Observable<GeneratedTemplateFromOffer> {
    return this.http.get<GeneratedTemplateFromOffer>(
      `${this.baseUrl}/generate-from-offer/${sessionId}`,
    );
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
