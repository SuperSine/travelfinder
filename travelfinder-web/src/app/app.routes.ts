import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'home', loadComponent: () => import('./pages/home/home.page').then(m => m.HomePage) },
  { path: 'plan', loadComponent: () => import('./pages/plan/plan.page').then(m => m.PlanPage) },
  {
    path: 'detail',
    loadComponent: () =>
      import('./components/plan-detail/plan-detail.component').then(m => m.PlanDetailComponent)
  }
];
