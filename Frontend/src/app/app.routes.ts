import { Routes } from '@angular/router';
import { CallbackAuth } from './callback-auth/callback-auth';
import { Home } from './home/home';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: Home },
  { path: 'callback/oauth', component: CallbackAuth },
];
