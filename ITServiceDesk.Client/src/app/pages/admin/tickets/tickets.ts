import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { DatePipe } from '@angular/common';
import { RoleBackButton } from '../../../shared/role-back-button/role-back-button';

import {
  TicketService,
  Ticket,
  UpdateTicketRequest,
  TicketAttachment
} from '../../../services/ticket';

interface TicketView {
  id: number;
  title: string;
  description: string;
  category: string;
  categoryId: number;
  priority: string;
  priorityId: number;
  status: string;
  created: string;
}

interface Option {
  id: number;
  name: string;
  isActive: boolean;
}

@Component({
  selector: 'app-tickets',
  standalone: true,
  imports: [FormsModule, RouterLink, DatePipe, RoleBackButton],
  templateUrl: './tickets.html',
  styleUrl: './tickets.css'
})
export class Tickets implements OnInit {
  private apiUrl = 'http://localhost:5204/api';

  searchText = '';
  selectedStatus = 'All';
  selectedPriority = 'All';

  tickets: TicketView[] = [];
  selectedTicket: TicketView | null = null;

  categories: Option[] = [];
  priorities: Option[] = [];

  isEditMode = false;
  isLoading = false;
  isUpdating = false;
  isDeleting = false;

  errorMessage = '';
  updateError = '';
  updateSuccess = '';

  // Attachments
  attachments: TicketAttachment[] = [];
  isLoadingAttachments = false;
  attachmentError = '';

  editForm: UpdateTicketRequest = {
    title: '',
    description: '',
    categoryId: 0,
    priorityId: 0,
    status: 'Open'
  };

  constructor(
    private ticketService: TicketService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadTickets();
    this.loadCategories();
    this.loadPriorities();
  }

  loadTickets(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.ticketService.getAllTickets().pipe(
      finalize(() => {
        this.isLoading = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: (response: Ticket[]) => {
        this.tickets = (response || []).map(ticket => ({
          id: ticket.id,
          title: ticket.title,
          description: ticket.description,
          category: ticket.categoryName,
          categoryId: ticket.categoryId,
          priority: ticket.priorityName,
          priorityId: ticket.priorityId,
          status: ticket.status,
          created: ticket.createdAt
            ? new Date(ticket.createdAt).toLocaleDateString('en-GB', {
                day: '2-digit',
                month: 'short',
                year: 'numeric'
              })
            : 'N/A'
        }));

        this.cdr.markForCheck();
      },
      error: error => {
        console.error('Ticket API error:', error);
        this.errorMessage =
          error.status === 401 || error.status === 403
            ? 'Please login again. You are not authorized to view tickets.'
            : 'Unable to load tickets. Please check the backend connection.';
        this.cdr.markForCheck();
      }
    });
  }

  loadCategories(): void {
    this.http.get<Option[]>(`${this.apiUrl}/Categories`).subscribe({
      next: response => {
        this.categories = (response || []).filter(item => item.isActive);
        this.cdr.markForCheck();
      },
      error: error => console.error('Categories loading error:', error)
    });
  }

  loadPriorities(): void {
    this.http.get<Option[]>(`${this.apiUrl}/Priorities`).subscribe({
      next: response => {
        this.priorities = (response || []).filter(item => item.isActive);
        this.cdr.markForCheck();
      },
      error: error => console.error('Priorities loading error:', error)
    });
  }

  openTicket(ticket: TicketView): void {
    this.selectedTicket = ticket;
    this.isEditMode = false;
    this.updateError = '';
    this.updateSuccess = '';
    this.resetEditForm(ticket);

    this.loadAttachments(ticket.id);
  }

  closeTicket(): void {
    this.selectedTicket = null;
    this.isEditMode = false;
    this.updateError = '';
    this.updateSuccess = '';
    this.attachments = [];
    this.attachmentError = '';
  }

  startEditing(): void {
    if (!this.selectedTicket) return;

    this.resetEditForm(this.selectedTicket);
    this.isEditMode = true;
    this.updateError = '';
    this.updateSuccess = '';
  }

  cancelEditing(): void {
    this.isEditMode = false;
    this.updateError = '';

    if (this.selectedTicket) {
      this.resetEditForm(this.selectedTicket);
    }
  }

  private resetEditForm(ticket: TicketView): void {
    this.editForm = {
      title: ticket.title,
      description: ticket.description,
      categoryId: ticket.categoryId,
      priorityId: ticket.priorityId,
      status: ticket.status
    };
  }

  saveTicket(): void {
    if (!this.selectedTicket || this.isUpdating) return;

    if (
      this.editForm.title.trim().length < 5 ||
      this.editForm.description.trim().length < 10 ||
      this.editForm.categoryId <= 0 ||
      this.editForm.priorityId <= 0
    ) {
      this.updateError = 'Please enter valid ticket details in all fields.';
      return;
    }

    const ticketId = this.selectedTicket.id;
    const originalTicket = this.selectedTicket;

    const request: UpdateTicketRequest = {
      ...this.editForm,
      title: this.editForm.title.trim(),
      description: this.editForm.description.trim()
    };

    this.isUpdating = true;
    this.updateError = '';
    this.updateSuccess = '';

    this.ticketService.updateTicket(ticketId, request).pipe(
      finalize(() => {
        this.isUpdating = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: () => {
        const updated: TicketView = {
          ...originalTicket,
          title: request.title,
          description: request.description,
          categoryId: request.categoryId,
          category: this.categories.find(
            x => x.id === request.categoryId
          )?.name ?? originalTicket.category,
          priorityId: request.priorityId,
          priority: this.priorities.find(
            x => x.id === request.priorityId
          )?.name ?? originalTicket.priority,
          status: request.status
        };

        this.tickets = this.tickets.map(ticket =>
          ticket.id === ticketId ? updated : ticket
        );

        if (this.selectedTicket?.id === ticketId) {
          this.selectedTicket = updated;
        }

        this.isEditMode = false;
        this.updateSuccess = 'Ticket updated successfully!';
        this.cdr.markForCheck();
      },
      error: error => {
        console.error('Ticket update error:', error);
        this.updateError =
          error.error?.message ||
          error.error?.Message ||
          'Unable to update ticket. Please try again.';
        this.cdr.markForCheck();
      }
    });
  }

  deleteTicket(): void {
    if (!this.selectedTicket || this.isDeleting) return;

    const ticketId = this.selectedTicket.id;

    const confirmed = window.confirm(
      `Are you sure you want to delete ticket #TKT-${ticketId}? This action cannot be undone.`
    );

    if (!confirmed) return;

    this.isDeleting = true;
    this.updateError = '';

    this.ticketService.deleteTicket(ticketId).pipe(
      finalize(() => {
        this.isDeleting = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: () => {
        this.tickets = this.tickets.filter(
          ticket => ticket.id !== ticketId
        );

        this.closeTicket();
        this.cdr.markForCheck();
      },
      error: error => {
        console.error('Ticket deletion error:', error);
        this.updateError =
          error.error?.message ||
          error.error?.Message ||
          'Unable to delete ticket. Please try again.';
        this.cdr.markForCheck();
      }
    });
  }

  loadAttachments(ticketId: number): void {
    this.isLoadingAttachments = true;
    this.attachmentError = '';

    this.ticketService.getAttachments(ticketId).pipe(
      finalize(() => {
        this.isLoadingAttachments = false;
        this.cdr.markForCheck();
      })
    ).subscribe({
      next: response => {
        this.attachments = response || [];
        this.cdr.markForCheck();
      },
      error: error => {
        console.error('Attachments loading error:', error);
        this.attachmentError = 'Unable to load attachments.';
        this.cdr.markForCheck();
      }
    });
  }

  downloadFile(attachment: TicketAttachment): void {
    if (!this.selectedTicket) return;

    this.ticketService.downloadAttachment(
      this.selectedTicket.id,
      attachment.id
    ).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');

        link.href = url;
        link.download = attachment.fileName;
        link.click();

        window.URL.revokeObjectURL(url);
      },
      error: error => {
        console.error('Attachment download error:', error);
        this.attachmentError = 'Unable to download this file.';
        this.cdr.markForCheck();
      }
    });
  }

  countStatus(status: string): number {
    return this.tickets.filter(
      ticket => ticket.status === status
    ).length;
  }

  get filteredTickets(): TicketView[] {
    const search = this.searchText.trim().toLowerCase();

    return this.tickets.filter(ticket => {
      const matchesSearch =
        ticket.title.toLowerCase().includes(search) ||
        ticket.description.toLowerCase().includes(search) ||
        ticket.id.toString().includes(search);

      const matchesStatus =
        this.selectedStatus === 'All' ||
        ticket.status === this.selectedStatus;

      const matchesPriority =
        this.selectedPriority === 'All' ||
        ticket.priority === this.selectedPriority;

      return matchesSearch && matchesStatus && matchesPriority;
    });
  }
}
