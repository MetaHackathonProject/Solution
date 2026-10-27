import { Routes } from '@angular/router';
import { CallbackAuth } from './callback-auth/callback-auth';
import { Home } from './home/home';
import { InterviewDetail } from './interview-detail/interview-detail';
import { Interviews } from './interviews/interviews';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: Home },
  { path: 'interviews', component: Interviews },
  {
    path: 'interviews/:sessionId/play',
    loadComponent: () =>
      import('./interview-play/interview-play').then((m) => m.InterviewPlay),
  },
  { path: 'interviews/:sessionId', component: InterviewDetail },
  { path: 'callback/oauth', component: CallbackAuth },
];
