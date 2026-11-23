
import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TicketService, Ticket } from '../../../services/ticket';
import { RoleBackButton } from '../../../shared/role-back-button/role-back-button';

interface Assignment {
  id: number;
  ticketId: number;
  assignedToUserId: string;
  assignedByUserId: string;
  assignedAt: string;
  unassignedAt: string | null;
}

@Component({
  selector: 'app-assigned-tickets',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, RoleBackButton],
  templateUrl: './assigned-tickets.html',
  styleUrl: './assigned-tickets.css'
})
export class AssignedTickets implements OnInit {
  private apiUrl = 'http://localhost:5204/api/Assignments';

  assignments: Assignment[] = [];
  tickets: Ticket[] = [];

  selectedTicketId: number | null = null;
  assignedToUserId = '';

  isLoading = false;
  isSaving = false;
  errorMessage = '';
  successMessage = '';

  searchText = '';
  statusFilter = 'All';

  constructor(
    private http: HttpClient,
    private ticketService: TicketService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loadTickets();
    this.loadAssignments();
  }

  loadTickets(): void {
    this.ticketService.getAllTickets().subscribe({
      next: response => {
        this.tickets = response || [];
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Tickets loading error:', error);
        this.errorMessage = 'Unable to load tickets.';
        this.cdr.detectChanges();
      }
    });
  }

  loadAssignments(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.http.get<Assignment[]>(this.apiUrl).subscribe({
      next: response => {
        this.assignments = response || [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Assignments loading error:', error);
        this.errorMessage = 'Unable to load assignments.';
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get filteredAssignments(): Assignment[] {
    return this.assignments.filter(item => {
      const search = this.searchText.trim().toLowerCase();

      const matchesSearch =
        !search ||
        String(item.ticketId).includes(search) ||
        item.assignedToUserId.toLowerCase().includes(search) ||
        item.assignedByUserId.toLowerCase().includes(search);

      const isActive = !item.unassignedAt;

      const matchesStatus =
        this.statusFilter === 'All' ||
        (this.statusFilter === 'Active' && isActive) ||
        (this.statusFilter === 'Unassigned' && !isActive);

      return matchesSearch && matchesStatus;
    }).sort((a, b) =>
      new Date(b.assignedAt).getTime() -
      new Date(a.assignedAt).getTime()
    );
  }

  get activeCount(): number {
    return this.assignments.filter(a => !a.unassignedAt).length;
  }

  get unassignedCount(): number {
    return this.assignments.filter(a => !!a.unassignedAt).length;
  }

  assignTicket(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.selectedTicketId) {
      this.errorMessage = 'Please select a ticket.';
      return;
    }

    const userId = this.assignedToUserId.trim();

    if (!userId) {
      this.errorMessage = 'Please enter the User ID of the assignee.';
      return;
    }

    this.isSaving = true;

    this.http.post<Assignment>(this.apiUrl, {
      ticketId: this.selectedTicketId,
      assignedToUserId: userId
    }).subscribe({
      next: () => {
        this.isSaving = false;
        this.successMessage = 'Ticket assigned successfully.';
        this.selectedTicketId = null;
        this.assignedToUserId = '';
        this.loadAssignments();
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Assign ticket error:', error);
        this.isSaving = false;
        this.errorMessage =
          error.error?.message ||
          error.error?.Message ||
          'Unable to assign ticket. Check the Ticket ID and User ID.';
        this.cdr.detectChanges();
      }
    });
  }

  unassignTicket(assignment: Assignment): void {
    if (assignment.unassignedAt) return;

    const confirmed = confirm(
      `Unassign Ticket #${assignment.ticketId}?`
    );

    if (!confirmed) return;

    this.http.patch(
      `${this.apiUrl}/${assignment.id}/unassign`,
      {}
    ).subscribe({
      next: () => {
        this.successMessage =
          `Ticket #${assignment.ticketId} unassigned successfully.`;
        this.loadAssignments();
      },
      error: error => {
        console.error('Unassign ticket error:', error);
        this.errorMessage =
          error.error?.message ||
          error.error?.Message ||
          'Unable to unassign ticket.';
        this.cdr.detectChanges();
      }
    });
  }

  clearMessages(): void {
    this.successMessage = '';
    this.errorMessage = '';
  }
}
