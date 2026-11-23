import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Auth } from '../../../services/auth';
import { Ticket, TicketService } from '../../../services/ticket';
import { RoleBackButton } from '../../../shared/role-back-button/role-back-button';

@Component({
  selector: 'app-user-dashboard',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, RouterLinkActive, RoleBackButton],
  template: `
    <div class="app-container">
      <aside class="sidebar">
        <div class="brand">
          <div class="brand-icon">IT</div>
          <div><h4>ServiceDesk</h4><span>Support Portal</span></div>
        </div>
        <div class="menu-label">MY SERVICE DESK</div>
        <nav class="sidebar-nav">
          <a routerLink="/user/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" class="nav-item"><span>▦</span> Dashboard</a>
          <a routerLink="/user/tickets" routerLinkActive="active" class="nav-item"><span>▣</span> My Tickets</a>
          <a routerLink="/user/create-ticket" routerLinkActive="active" class="nav-item"><span>＋</span> Create Ticket</a>
        </nav>
        <div class="sidebar-bottom">
          <div class="user-profile">
            <div class="avatar">{{ initials }}</div>
            <div class="user-info"><strong>{{ fullName }}</strong><span>User</span></div>
          </div>
          <a href="/login" (click)="logout($event)" class="nav-item mt-2"><span>↪</span> Log out</a>
        </div>
      </aside>

      <main class="main-content">
        @if (showAll) { <app-role-back-button /> }
        <header class="topbar">
          <div><div class="breadcrumb">Pages / <strong>{{ showAll ? 'My Tickets' : 'Dashboard' }}</strong></div><h3>{{ showAll ? 'My Tickets' : 'User Dashboard' }}</h3></div>
          <div class="topbar-right">
            <button type="button" class="btn btn-outline-danger btn-sm" (click)="logout($event)">Logout</button>
            <div class="top-avatar">{{ initials }}</div>
          </div>
        </header>

        <section class="dashboard-content">
          <div class="welcome-section">
            <div><h2>Welcome back, {{ fullName }}!</h2><p>Track your support requests and their progress.</p></div>
            <button class="primary-btn" routerLink="/user/create-ticket"><span>+</span> Create New Ticket</button>
          </div>

          @if (isLoading) {
            <div class="text-center p-4">Loading your tickets...</div>
          } @else if (errorMessage) {
            <div class="alert alert-danger" role="alert">{{ errorMessage }} <button class="btn btn-sm btn-outline-danger ms-2" (click)="loadTickets()">Retry</button></div>
          } @else {
            <div class="stats-grid">
              <div class="stat-card"><div class="stat-top"><span>Total Tickets</span><div class="stat-icon blue">▣</div></div><h2>{{ tickets.length }}</h2><div class="stat-description">Tickets created by you</div></div>
              <div class="stat-card"><div class="stat-top"><span>Open</span><div class="stat-icon orange">◷</div></div><h2>{{ countStatus('Open') }}</h2><div class="stat-description">Waiting for support</div></div>
              <div class="stat-card"><div class="stat-top"><span>In Progress</span><div class="stat-icon purple">↻</div></div><h2>{{ countStatus('In Progress') }}</h2><div class="stat-description">Being handled</div></div>
              <div class="stat-card"><div class="stat-top"><span>Resolved</span><div class="stat-icon green">✓</div></div><h2>{{ countStatus('Resolved') }}</h2><div class="stat-description">Successfully resolved</div></div>
            </div>

            <section class="content-card">
              <div class="card-heading">
                <div><h4>{{ showAll ? 'All My Tickets' : 'Recent Tickets' }}</h4><p>Tickets submitted from your account</p></div>
                @if (!showAll && tickets.length > 5) { <a class="outline-btn text-decoration-none" routerLink="/user/tickets">View all tickets →</a> }
              </div>

              @if (tickets.length === 0) {
                <div class="text-center text-muted p-4">You have not created any tickets yet. Use “Create New Ticket” to submit a request.</div>
              } @else {
                <div class="table-responsive">
                  <table class="table ticket-table align-middle">
                    <thead><tr><th>Ticket ID</th><th>Title</th><th>Category</th><th>Priority</th><th>Status</th><th>Created</th><th></th></tr></thead>
                    <tbody>
                      @for (ticket of visibleTickets; track ticket.id) {
                        <tr>
                          <td>#TKT-{{ ticket.id }}</td>
                          <td><strong>{{ ticket.title }}</strong></td>
                          <td>{{ ticket.categoryName }}</td>
                          <td><span class="priority" [ngClass]="getPriorityClass(ticket.priorityName)">{{ ticket.priorityName }}</span></td>
                          <td><span class="status" [ngClass]="getStatusClass(ticket.status)">{{ ticket.status }}</span></td>
                          <td>{{ ticket.createdAt | date:'dd MMM yyyy' }}</td>
                          <td><button type="button" class="outline-btn" (click)="toggleDetails(ticket.id)">{{ selectedTicketId === ticket.id ? 'Hide details' : 'View details' }}</button></td>
                        </tr>
                        @if (selectedTicketId === ticket.id) {
                          <tr class="ticket-detail-row"><td colspan="7"><strong>Description</strong><p class="mb-1 mt-2">{{ ticket.description }}</p>@if (ticket.updatedAt) { <small class="text-muted">Last updated {{ ticket.updatedAt | date:'dd MMM yyyy, HH:mm' }}</small> }</td></tr>
                        }
                      }
                    </tbody>
                  </table>
                </div>
              }
            </section>
          }
          <footer class="footer"><span>© 2026 IT Service Desk Management System</span><span>Your tickets are private to your account</span></footer>
        </section>
      </main>
    </div>
  `,
  styleUrl: '../../admin/dashboard/dashboard.css'
})
export class UserDashboard implements OnInit {
  readonly fullName = localStorage.getItem('fullName') || 'User';
  readonly initials = this.fullName.split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase();
  tickets: Ticket[] = [];
  isLoading = true;
  errorMessage = '';
  selectedTicketId: number | null = null;
  showAll = false;

  constructor(
    private ticketService: TicketService,
    private auth: Auth,
    private router: Router,
    private cdr: ChangeDetectorRef,
    route: ActivatedRoute
  ) {
    this.showAll = route.snapshot.data['showAll'] === true;
  }

  ngOnInit(): void {
    this.loadTickets();
  }

  loadTickets(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.ticketService.getAllTickets().subscribe({
      next: tickets => {
        this.tickets = (tickets || []).sort((a, b) =>
          new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: error => {
        this.errorMessage = error.status === 401 || error.status === 403
          ? 'Please sign in again to view your tickets.'
          : 'Unable to load your tickets. Please try again.';
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  get visibleTickets(): Ticket[] {
    return this.showAll ? this.tickets : this.tickets.slice(0, 5);
  }

  countStatus(status: string): number {
    return this.tickets.filter(ticket => ticket.status?.toLowerCase() === status.toLowerCase()).length;
  }

  toggleDetails(ticketId: number): void {
    this.selectedTicketId = this.selectedTicketId === ticketId ? null : ticketId;
  }

  getPriorityClass(priority: string): string {
    switch (priority?.toLowerCase()) {
      case 'critical': return 'critical';
      case 'high': return 'high';
      case 'low': return 'low';
      default: return 'medium';
    }
  }

  getStatusClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'in progress': return 'progress';
      case 'resolved':
      case 'closed': return 'resolved';
      default: return 'open';
    }
  }

  logout(event: Event): void {
    event.preventDefault();
    this.auth.logout();
    void this.router.navigateByUrl('/login', { replaceUrl: true });
  }
}
