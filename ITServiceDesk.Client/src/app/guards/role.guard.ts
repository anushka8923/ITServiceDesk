import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from '../services/auth';

export function dashboardForRoles(roles: string[]): string {
  if (roles.includes('SuperAdmin')) return '/superadmin/dashboard';
  if (roles.includes('Admin')) return '/admin/dashboard';
  if (roles.includes('User')) return '/user/dashboard';
  return '/login';
}

export const roleGuard: CanActivateFn = route => {
  const auth = inject(Auth);
  const router = inject(Router);
  const roles = auth.getRoles();

  if (!auth.isLoggedIn()) return router.createUrlTree(['/login']);
  if ((route.data['roles'] as string[]).some(role => roles.includes(role))) {
    return true;
  }

  return router.createUrlTree([dashboardForRoles(roles)]);
};

export const dashboardRedirectGuard: CanActivateFn = () => {
  const auth = inject(Auth);
  const router = inject(Router);
  return router.createUrlTree([
    auth.isLoggedIn() ? dashboardForRoles(auth.getRoles()) : '/login'
  ]);
};
