
import { Routes } from '@angular/router';
import { dashboardRedirectGuard, roleGuard } from './guards/role.guard';

export const routes: Routes = [
  {
    path: 'register',
    pathMatch: 'full',
    loadComponent: () =>
      import('./pages/auth/register/register').then(m => m.Register)
  },
  {
    path: '',
    canMatch: [dashboardRedirectGuard],
    pathMatch: 'full',
    loadComponent: () =>
      import('./pages/auth/login/login').then(m => m.Login)
  },
  {
    path: 'admin/dashboard',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./pages/admin/dashboard/dashboard').then(m => m.Dashboard)
  },
  {
    path: 'admin/tickets',
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] },
    loadComponent: () =>
      import('./pages/admin/tickets/tickets').then(m => m.Tickets)
  },
  {
    path: 'admin/create-ticket',
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] },
    loadComponent: () =>
      import('./pages/admin/create-ticket/create-ticket').then(m => m.CreateTicket)
  },
  {
    path: 'admin/reports',
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] },
    loadComponent: () =>
      import('./pages/admin/reports/reports').then(m => m.Reports)
  },
  {
    path: 'admin/categories',
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] },
    loadComponent: () =>
      import('./pages/admin/categories/categories').then(m => m.Categories)
  },
  {
    path: 'admin/assigned-tickets',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./pages/admin/assigned-tickets/assigned-tickets')
        .then(m => m.AssignedTickets)
  },
  {
    path: 'admin/audit-logs',
    canActivate: [roleGuard],
    data: { roles: ['Admin', 'SuperAdmin'] },
    loadComponent: () =>
      import('./pages/admin/audit-logs/audit-logs')
        .then(m => m.AuditLogs)
  },
  {
    path: 'admin/users',
    canActivate: [roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./pages/user-management/user-management').then(m => m.UserManagement)
  },
  {
    path: 'superadmin/dashboard',
    canActivate: [roleGuard],
    data: { roles: ['SuperAdmin'] },
    loadComponent: () =>
      import('./pages/superadmin/dashboard/dashboard').then(m => m.SuperAdminDashboard)
  },
  {
    path: 'superadmin/users',
    canActivate: [roleGuard],
    data: { roles: ['SuperAdmin'] },
    loadComponent: () =>
      import('./pages/user-management/user-management').then(m => m.UserManagement)
  },
  {
    path: 'user/dashboard',
    canActivate: [roleGuard],
    data: { roles: ['User'] },
    loadComponent: () =>
      import('./pages/user/dashboard/dashboard').then(m => m.UserDashboard)
  },
  {
    path: 'user/tickets',
    canActivate: [roleGuard],
    data: { roles: ['User'], showAll: true },
    loadComponent: () =>
      import('./pages/user/dashboard/dashboard').then(m => m.UserDashboard)
  },
  {
    path: 'user/create-ticket',
    canActivate: [roleGuard],
    data: { roles: ['User'] },
    loadComponent: () =>
      import('./pages/admin/create-ticket/create-ticket').then(m => m.CreateTicket)
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/auth/login/login').then(m => m.Login)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
