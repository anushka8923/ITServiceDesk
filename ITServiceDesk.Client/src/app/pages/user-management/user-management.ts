import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Auth, ManagedUser } from '../../services/auth';
import { finalize } from 'rxjs';
import { RoleBackButton } from '../../shared/role-back-button/role-back-button';

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule, RoleBackButton],
  template: `
    <main class="container py-5">
      <app-role-back-button />
      <div class="mb-4">
        <p class="text-muted mb-1">IT Service Desk</p>
        <h1 class="h3 mb-0">User Management</h1>
      </div>
      @if (message) { <div class="alert alert-success">{{ message }}</div> }
      @if (errorMessage) { <div class="alert alert-danger">{{ errorMessage }}</div> }
      <section class="card shadow-sm">
        <div class="card-body">
          @if (isLoading) {
            <p class="text-muted mb-0">Loading accounts...</p>
          } @else if (users.length === 0) {
            <p class="text-muted mb-0">No accounts found.</p>
          } @else {
            <div class="table-responsive">
              <table class="table align-middle mb-0">
                <thead><tr><th>Name</th><th>Email</th><th>Role</th>@if (isSuperAdmin) { <th>Manage role</th> }</tr></thead>
                <tbody>
                  @for (user of users; track user.userId) {
                    <tr>
                      <td>{{ user.fullName }}</td><td>{{ user.email }}</td><td>{{ user.roles.join(', ') }}</td>
                      @if (isSuperAdmin) {
                        <td>
                          @if (!user.roles.includes('SuperAdmin')) {
                            <div class="d-flex gap-2">
                              <select class="form-select form-select-sm" [(ngModel)]="selectedRoles[user.userId]" [name]="'role-' + user.userId" aria-label="Account role">
                                <option value="User">User</option><option value="Admin">Admin</option>
                              </select>
                              <button class="btn btn-sm btn-primary" [disabled]="savingUserId === user.userId || selectedRoles[user.userId] === user.roles[0]" (click)="saveRole(user)">
                                {{ savingUserId === user.userId ? 'Saving...' : 'Save' }}
                              </button>
                            </div>
                          } @else { <span class="text-muted">Protected account</span> }
                        </td>
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </section>
    </main>
  `
})
export class UserManagement implements OnInit {
  users: ManagedUser[] = [];
  selectedRoles: Record<string, 'Admin' | 'User'> = {};
  isLoading = true;
  isSuperAdmin = false;
  savingUserId = '';
  message = '';
  errorMessage = '';

  constructor(private auth: Auth, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.isSuperAdmin = this.auth.getRoles().includes('SuperAdmin');
    this.loadUsers();
  }

  loadUsers(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.auth.getUsers().pipe(
      finalize(() => {
        this.isLoading = false;
        this.cdr.detectChanges();
      })
    ).subscribe({
      next: response => {
        const users = Array.isArray(response) ? response : [];
        this.users = users;
        for (const user of users) {
          this.selectedRoles[user.userId] = user.roles.includes('Admin') ? 'Admin' : 'User';
        }
        this.cdr.detectChanges();
      },
      error: error => {
        this.errorMessage = error.error?.Message || error.error?.message ||
          (error.status === 401 || error.status === 403
            ? 'You are not authorized to view these accounts. Please sign in again with an authorized account.'
            : 'Unable to load accounts. Please try again.');
        this.cdr.detectChanges();
      }
    });
  }

  saveRole(user: ManagedUser): void {
    const role = this.selectedRoles[user.userId];
    if (!this.isSuperAdmin || !role || user.roles.includes('SuperAdmin')) return;

    this.errorMessage = '';
    this.message = '';
    this.savingUserId = user.userId;
    this.auth.updateUserRole(user.userId, role).subscribe({
      next: response => {
        this.message = response.message;
        this.savingUserId = '';
        this.cdr.detectChanges();
        this.loadUsers();
      },
      error: error => {
        this.errorMessage = error.error?.Message || error.error?.message || 'Unable to update account role.';
        this.savingUserId = '';
        this.cdr.detectChanges();
      }
    });
  }
}
