
import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { RoleBackButton } from '../../../shared/role-back-button/role-back-button';

interface AuditLog {
  id: number;
  userId: string;
  action: string;
  details: string;
  createdAt: string;
}

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, RoleBackButton],
  templateUrl: './audit-logs.html',
  styleUrl: './audit-logs.css'
})
export class AuditLogs implements OnInit {

  private apiUrl = 'http://localhost:5204/api/AuditLogs';

  logs: AuditLog[] = [];
  isLoading = false;
  errorMessage = '';

  searchText = '';
  actionFilter = 'All';
  fromDate = '';
  toDate = '';

  constructor(private http: HttpClient, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    if (this.isLoading) return;

    this.isLoading = true;
    this.errorMessage = '';
    this.cdr.markForCheck();

    this.http.get<AuditLog[]>(this.apiUrl).pipe(
      finalize(() => {
        this.isLoading = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: (data) => {
        this.logs = Array.isArray(data) ? data : [];
        this.cdr.markForCheck();
      },
      error: (error) => {
        console.error('Audit logs error:', error);
        this.errorMessage =
          error.status === 401 || error.status === 403
            ? 'Access denied. Please login with an Admin account.'
            : 'Unable to load audit logs. Please check whether the API is running.';
        this.cdr.markForCheck();
      }
    });
  }

  get actionOptions(): string[] {
    return [...new Set(
      this.logs
        .map(log => log.action)
        .filter(action => !!action)
    )];
  }

  get filteredLogs(): AuditLog[] {
    const search = this.searchText.trim().toLowerCase();

    return this.logs.filter(log => {
      const matchesSearch =
        !search ||
        (log.userId || '').toLowerCase().includes(search) ||
        (log.action || '').toLowerCase().includes(search) ||
        (log.details || '').toLowerCase().includes(search) ||
        String(log.id).includes(search);

      const matchesAction =
        this.actionFilter === 'All' ||
        log.action === this.actionFilter;

      const logDate = log.createdAt
        ? new Date(log.createdAt).toISOString().slice(0, 10)
        : '';

      const matchesFromDate =
        !this.fromDate || logDate >= this.fromDate;

      const matchesToDate =
        !this.toDate || logDate <= this.toDate;

      return matchesSearch &&
        matchesAction &&
        matchesFromDate &&
        matchesToDate;
    });
  }

  get totalLogs(): number {
    return this.logs.length;
  }

  get todayLogs(): number {
    const today = new Date().toDateString();

    return this.logs.filter(log =>
      new Date(log.createdAt).toDateString() === today
    ).length;
  }

  get uniqueUsers(): number {
    return new Set(
      this.logs.map(log => log.userId).filter(Boolean)
    ).size;
  }

  resetFilters(): void {
    this.searchText = '';
    this.actionFilter = 'All';
    this.fromDate = '';
    this.toDate = '';
    this.loadLogs();
  }
}
