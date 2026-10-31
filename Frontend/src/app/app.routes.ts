import { Routes } from '@angular/router';
import { CallbackAuth } from './callback-auth/callback-auth';
import { EmployerAssignments } from './employer-assignments/employer-assignments';
import { EmployerTemplateEdit } from './employer-template-edit/employer-template-edit';
import { EmployerTemplates } from './employer-templates/employer-templates';
import { Home } from './home/home';
import { InterviewDetail } from './interview-detail/interview-detail';
import { Interviews } from './interviews/interviews';
import { JoinInterview } from './join-interview/join-interview';
import { Welcome } from './welcome/welcome';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: Home },
  { path: 'welcome', component: Welcome },
  { path: 'templates/new', component: EmployerTemplateEdit },
  { path: 'templates/:templateId', component: EmployerTemplateEdit },
  { path: 'templates', component: EmployerTemplates },
  { path: 'assignments', component: EmployerAssignments },
  { path: 'join/:inviteCode', component: JoinInterview },
  { path: 'interviews', component: Interviews },
  {
    path: 'interviews/:sessionId/play',
    loadComponent: () =>
      import('./interview-play/interview-play').then((m) => m.InterviewPlay),
  },
  { path: 'interviews/:sessionId', component: InterviewDetail },
  { path: 'callback/oauth', component: CallbackAuth },
];
