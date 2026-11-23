import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Auth } from '../../../services/auth';

interface DashboardStats {
  totalTickets: number;
  openTickets: number;
  inProgressTickets: number;
  resolvedTickets: number;
  closedTickets: number;
}

@Component({
  selector: 'app-superadmin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  template: `
    <div class="app-container">
      <aside class="sidebar">
        <div class="brand">
          <div class="brand-icon">IT</div>
          <div><h4>ServiceDesk</h4><span>Company Portal</span></div>
        </div>
        <div class="menu-label">MAIN MENU</div>
        <nav class="sidebar-nav">
          <a routerLink="/superadmin/dashboard" routerLinkActive="active" class="nav-item"><span>▦</span> Dashboard</a>
          <a routerLink="/superadmin/users" routerLinkActive="active" class="nav-item"><span>♙</span> User Management</a>
          <a routerLink="/admin/tickets" class="nav-item"><span>▣</span> All Tickets</a>
          <div class="menu-label mt-4">OPERATIONS</div>
          <a routerLink="/admin/reports" class="nav-item"><span>▥</span> Reports &amp; Analytics</a>
          <a routerLink="/admin/categories" class="nav-item"><span>▧</span> Category Management</a>
          <a routerLink="/admin/audit-logs" class="nav-item"><span>◉</span> Audit Logs</a>
        </nav>
        <div class="sidebar-bottom">
          <div class="user-profile">
            <div class="avatar">{{ initials }}</div>
            <div class="user-info"><strong>{{ fullName }}</strong><span>SuperAdmin</span></div>
          </div>
          <a href="/login" (click)="logout($event)" class="nav-item mt-2"><span>↪</span> Log out</a>
        </div>
      </aside>

      <main class="main-content">
        <header class="topbar">
          <div><div class="breadcrumb">Pages / <strong>Dashboard</strong></div><h3>SuperAdmin Dashboard</h3></div>
          <div class="topbar-right"><div class="top-avatar">{{ initials }}</div></div>
        </header>
        <section class="dashboard-content">
          <div class="welcome-section">
            <div><h2>Welcome back, {{ fullName }}!</h2><p>Company-wide service desk overview.</p></div>
            <button class="primary-btn" routerLink="/superadmin/users">Manage Accounts</button>
          </div>

          <div class="stats-grid">
            @if (accountsLoading) {
              <div class="stat-card text-center p-4">Loading account statistics...</div>
            } @else if (accountsErrorMessage) {
              <div class="stat-card"><div class="alert alert-danger mb-0" role="alert">{{ accountsErrorMessage }} <button class="btn btn-sm btn-outline-danger ms-2" (click)="loadAccounts()">Retry</button></div></div>
            } @else {
              <div class="stat-card"><div class="stat-top"><span>User Accounts</span><div class="stat-icon blue">&#9787;</div></div><h2>{{ userCount }}</h2><div class="stat-description">Normal user accounts</div></div>
              <div class="stat-card"><div class="stat-top"><span>Administrators</span><div class="stat-icon purple">&#9812;</div></div><h2>{{ adminCount }}</h2><div class="stat-description">Admin role accounts</div></div>
            }
            @if (ticketStatsLoading) {
              <div class="stat-card text-center p-4">Loading ticket statistics...</div>
            } @else if (ticketStatsErrorMessage) {
              <div class="stat-card"><div class="alert alert-danger mb-0" role="alert">{{ ticketStatsErrorMessage }} <button class="btn btn-sm btn-outline-danger ms-2" (click)="loadTicketStats()">Retry</button></div></div>
            } @else {
              <div class="stat-card"><div class="stat-top"><span>Total Tickets</span><div class="stat-icon blue">&#9638;</div></div><h2>{{ stats.totalTickets }}</h2><div class="stat-description">Across the service desk</div></div>
              <div class="stat-card"><div class="stat-top"><span>Open Tickets</span><div class="stat-icon orange">&#9675;</div></div><h2>{{ stats.openTickets }}</h2><div class="stat-description">Waiting for resolution</div></div>
              <div class="stat-card"><div class="stat-top"><span>In Progress</span><div class="stat-icon purple">&#8635;</div></div><h2>{{ stats.inProgressTickets }}</h2><div class="stat-description">Currently being handled</div></div>
              <div class="stat-card"><div class="stat-top"><span>Resolved</span><div class="stat-icon green">&#10003;</div></div><h2>{{ stats.resolvedTickets }}</h2><div class="stat-description">Successfully resolved</div></div>
            }
          </div>
          <section class="content-card mt-4">
            <div class="card-heading"><div><h4>Company controls</h4><p>Manage accounts and review service desk activity.</p></div></div>
            <div class="d-flex flex-wrap gap-2">
              <a class="outline-btn text-decoration-none" routerLink="/superadmin/users">User Management</a>
              <a class="outline-btn text-decoration-none" routerLink="/admin/tickets">All Tickets</a>
              <a class="outline-btn text-decoration-none" routerLink="/admin/reports">Reports &amp; Analytics</a>
              <a class="outline-btn text-decoration-none" routerLink="/admin/categories">Category Management</a>
              <a class="outline-btn text-decoration-none" routerLink="/admin/audit-logs">Audit Logs</a>
            </div>
          </section>
          <div class="alert alert-primary py-2 px-3 mt-3 mb-0 small" role="note">Shared SuperAdmin demo account: superadmin@itservicedesk.com</div>
          <footer class="footer"><span>© 2026 IT Service Desk Management System</span><span>Company administration</span></footer>
        </section>
      </main>
    </div>
  `,
  styleUrl: '../../admin/dashboard/dashboard.css'
})
export class SuperAdminDashboard implements OnInit {
  readonly fullName = localStorage.getItem('fullName') || 'SuperAdmin';
  readonly initials = this.fullName.split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase();
  userCount = 0;
  adminCount = 0;
  stats: DashboardStats = { totalTickets: 0, openTickets: 0, inProgressTickets: 0, resolvedTickets: 0, closedTickets: 0 };
  accountsLoading = true;
  ticketStatsLoading = true;
  accountsErrorMessage = '';
  ticketStatsErrorMessage = '';
  private accountsRequestInProgress = false;
  private ticketStatsRequestInProgress = false;

  constructor(
    private auth: Auth,
    private router: Router,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.loadAccounts();
    this.loadTicketStats();
  }

  loadAccounts(): void {
    if (this.accountsRequestInProgress) return;
    this.accountsRequestInProgress = true;
    this.accountsLoading = true;
    this.accountsErrorMessage = '';
    this.auth.getUsers().subscribe({
      next: users => {
        this.userCount = users.filter(user => user.roles.includes('User')).length;
        this.adminCount = users.filter(user => user.roles.includes('Admin')).length;
        this.accountsLoading = false;
        this.accountsRequestInProgress = false;
        this.cdr.markForCheck();
      },
      error: error => {
        this.accountsErrorMessage = error.status === 401 || error.status === 403
          ? 'You are not authorized to view company dashboard data.'
          : 'Unable to load company dashboard data.';
        this.accountsLoading = false;
        this.accountsRequestInProgress = false;
        this.cdr.markForCheck();
      }
    });
  }

  loadTicketStats(): void {
    if (this.ticketStatsRequestInProgress) return;
    this.ticketStatsRequestInProgress = true;
    this.ticketStatsLoading = true;
    this.ticketStatsErrorMessage = '';
    this.http.get<DashboardStats>('http://localhost:5204/api/Admin/dashboard').subscribe({
      next: stats => {
        this.stats = stats;
        this.ticketStatsLoading = false;
        this.ticketStatsRequestInProgress = false;
        this.cdr.markForCheck();
      },
      error: error => {
        this.ticketStatsErrorMessage = error.status === 401 || error.status === 403
          ? 'You are not authorized to view company dashboard data.'
          : 'Unable to load company dashboard data.';
        this.ticketStatsLoading = false;
        this.ticketStatsRequestInProgress = false;
        this.cdr.markForCheck();
      }
    });
  }

  logout(event: Event): void {
    event.preventDefault();
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
