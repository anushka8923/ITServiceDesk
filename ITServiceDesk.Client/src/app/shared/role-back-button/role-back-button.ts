import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Auth } from '../../services/auth';

@Component({
  selector: 'app-role-back-button',
  standalone: true,
  imports: [RouterLink],
  template: `<a class="role-back-button" [routerLink]="dashboardUrl">← Back</a>`,
  styles: [`
    :host {
      display: block;
      width: fit-content;
      max-width: 100%;
      margin-bottom: 1rem;
    }

    .role-back-button {
      display: inline-flex;
      align-items: center;
      min-height: 40px;
      padding: 0.45rem 0.9rem;
      border: 1px solid #d5dbe5;
      border-radius: 0.5rem;
      background: #fff;
      color: #344158;
      font-size: 0.9rem;
      font-weight: 600;
      line-height: 1.2;
      text-decoration: none;
      transition: background-color 120ms ease, border-color 120ms ease;
    }

    .role-back-button:hover {
      background: #f4f6fa;
      border-color: #b8c2d1;
      color: #243047;
    }

    .role-back-button:focus-visible {
      outline: 3px solid rgba(76, 111, 255, 0.3);
      outline-offset: 2px;
    }

    @media (max-width: 576px) {
      :host { margin-bottom: 0.75rem; }
      .role-back-button { min-height: 38px; padding-inline: 0.75rem; }
    }
  `]
})
export class RoleBackButton {
  constructor(private auth: Auth) {}

  get dashboardUrl(): string {
    const roles = this.auth.getRoles();
    if (roles.includes('SuperAdmin')) return '/superadmin/dashboard';
    if (roles.includes('Admin')) return '/admin/dashboard';
    return '/user/dashboard';
  }
}
