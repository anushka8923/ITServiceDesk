
import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
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

interface DashboardTicket {
  id: number;
  title: string;
  description: string;
  status: string;
  createdAt: string;
  categoryName: string;
  priorityName: string;
}

interface CategoryStat {
  categoryName: string;
  ticketCount: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterLink,
    RouterLinkActive
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class Dashboard implements OnInit {

  private apiUrl = 'http://localhost:5204/api';

  stats: DashboardStats = {
    totalTickets: 0,
    openTickets: 0,
    inProgressTickets: 0,
    resolvedTickets: 0,
    closedTickets: 0
  };

  recentTickets: DashboardTicket[] = [];
  categoryStats: CategoryStat[] = [];

  isLoading = true;
  errorMessage = '';

  constructor(
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
    private auth: Auth,
    private router: Router
  ) {}

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login', { replaceUrl: true });
  }

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading = true;
    this.errorMessage = '';

    // 1. Load dashboard statistics
    this.http.get<DashboardStats>(
      `${this.apiUrl}/Admin/dashboard`
    ).subscribe({
      next: response => {
        this.stats = response;
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Dashboard stats error:', error);

        this.errorMessage =
          error.status === 401 || error.status === 403
            ? 'You are not authorized. Please login again.'
            : 'Unable to load dashboard statistics.';

        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });

    // 2. Load category statistics
    this.http.get<CategoryStat[]>(
      `${this.apiUrl}/Admin/category-stats`
    ).subscribe({
      next: response => {
        this.categoryStats = response || [];
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Category stats error:', error);
      }
    });

    // 3. Load recent tickets
    this.http.get<DashboardTicket[]>(
      `${this.apiUrl}/Tickets`
    ).subscribe({
      next: response => {
        this.recentTickets = (response || [])
          .sort((a, b) =>
            new Date(b.createdAt).getTime() -
            new Date(a.createdAt).getTime()
          )
          .slice(0, 5);

        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Recent tickets error:', error);
      }
    });
  }

  get openPercentage(): number {
    return this.getPercentage(this.stats.openTickets);
  }

  get progressPercentage(): number {
    return this.getPercentage(this.stats.inProgressTickets);
  }

  get resolvedPercentage(): number {
    return this.getPercentage(this.stats.resolvedTickets);
  }

  getPercentage(value: number): number {
    if (!this.stats.totalTickets) {
      return 0;
    }

    return Math.round(
      (value / this.stats.totalTickets) * 100
    );
  }

  getPriorityClass(priority: string): string {
    switch (priority?.toLowerCase()) {
      case 'critical':
        return 'critical';

      case 'high':
        return 'high';

      case 'medium':
        return 'medium';

      case 'low':
        return 'low';

      default:
        return 'medium';
    }
  }

  getStatusClass(status: string): string {
    switch (status?.toLowerCase()) {
      case 'open':
        return 'open';

      case 'in progress':
        return 'progress';

      case 'resolved':
        return 'resolved';

      case 'closed':
        return 'resolved';

      default:
        return 'open';
    }
  }
}
