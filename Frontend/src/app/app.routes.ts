import { Routes } from '@angular/router';
import { CallbackAuth } from './callback-auth/callback-auth';
import { Home } from './home/home';
import { Interviews } from './interviews/interviews';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: Home },
  { path: 'interviews', component: Interviews },
  { path: 'callback/oauth', component: CallbackAuth },
];
