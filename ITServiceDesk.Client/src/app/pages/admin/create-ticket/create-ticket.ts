
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Location } from '@angular/common';
import { Auth } from '../../../services/auth';
import { Ticket, TicketService } from '../../../services/ticket';
import { RoleBackButton } from '../../../shared/role-back-button/role-back-button';

interface Category {
  id: number;
  name: string;
  description?: string;
  isActive: boolean;
}

interface Priority {
  id: number;
  name: string;
  description?: string;
  isActive: boolean;
}

@Component({
  selector: 'app-create-ticket',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, RoleBackButton],
  templateUrl: './create-ticket.html',
  styleUrl: './create-ticket.css'
})
export class CreateTicket implements OnInit {

  private apiUrl = 'http://localhost:5204/api';
  private auth: Auth;

  title = '';
  description = '';
  categoryId = 0;
  priorityId = 0;
  selectedFile: File | null = null;
  isUserCreate = false;

  categories: Category[] = [];
  priorities: Priority[] = [];

  isLoading = false;
  isSubmitting = false;
  errorMessage = '';
  successMessage = '';

  constructor(
  private http: HttpClient,
  private router: Router,
  private location: Location,
  auth: Auth,
  private ticketService: TicketService
) {
  this.auth = auth;
  this.isUserCreate = auth.getRoles().includes('User');
}

goBack(): void {
  this.location.back();
}

  ngOnInit(): void {
    this.loadCategories();
    this.loadPriorities();
  }

  loadCategories(): void {
    this.http.get<Category[]>(`${this.apiUrl}/Categories`)
      .subscribe({
        next: (response) => {
          this.categories = response.filter(item => item.isActive);
        },
        error: (error) => {
          console.error('Category loading error:', error);
          this.errorMessage = 'Unable to load categories.';
        }
      });
  }

  loadPriorities(): void {
    this.http.get<Priority[]>(`${this.apiUrl}/Priorities`)
      .subscribe({
        next: (response) => {
          this.priorities = response.filter(item => item.isActive);
        },
        error: (error) => {
          console.error('Priority loading error:', error);
          this.errorMessage = 'Unable to load priorities.';
        }
      });
  }

  onFileSelected(event: Event): void {
      const input = event.target as HTMLInputElement;
      const file = input.files?.[0] ?? null;

      this.selectedFile = file;
      this.errorMessage = '';

      if (!file) return;

      const allowedExtensions = [
        '.jpg', '.jpeg', '.png', '.pdf', '.doc', '.docx'
      ];
      const extension = file.name
        .substring(file.name.lastIndexOf('.'))
        .toLowerCase();

      if (!allowedExtensions.includes(extension)) {
        this.errorMessage = 'Only JPG, PNG, PDF, DOC and DOCX files are allowed.';
        this.selectedFile = null;
        input.value = '';
        return;
      }

      if (file.size > 5 * 1024 * 1024) {
        this.errorMessage = 'File size cannot exceed 5 MB.';
        this.selectedFile = null;
        input.value = '';
      }
  }

  formatFileSize(bytes: number): string {
      if (bytes < 1024) return `${bytes} B`;
      if (bytes < 1024 * 1024) {
        return `${(bytes / 1024).toFixed(1)} KB`;
      }
      return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  createTicket(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (
      this.title.trim().length < 5 ||
      this.title.trim().length > 200
    ) {
      this.errorMessage = 'Title must be between 5 and 200 characters.';
      return;
    }

    if (
      this.description.trim().length < 10 ||
      this.description.trim().length > 2000
    ) {
      this.errorMessage = 'Description must be between 10 and 2000 characters.';
      return;
    }

    if (this.categoryId <= 0 || this.priorityId <= 0) {
      this.errorMessage = 'Please select a category and priority.';
      return;
    }

    this.isSubmitting = true;

    const ticketData = {
      title: this.title.trim(),
      description: this.description.trim(),
      categoryId: Number(this.categoryId),
      priorityId: Number(this.priorityId)
    };

    this.http.post<Ticket>(`${this.apiUrl}/Tickets`, ticketData)
      .subscribe({
        next: ticket => {
          const file = this.selectedFile;

          if (!file) {
            this.completeTicketCreation();
            return;
          }

          this.ticketService.uploadAttachment(ticket.id, file)
            .subscribe({
              next: () => this.completeTicketCreation(),
              error: error => {
                this.isSubmitting = false;
                this.successMessage =
                  `Ticket #TKT-${ticket.id} created successfully.`;
                this.errorMessage =
                  error.error?.Message ||
                  error.error?.message ||
                  `Ticket #TKT-${ticket.id} was created, but the attachment upload failed.`;
              }
            });
        },
        error: (error) => {
          this.isSubmitting = false;
          console.error('Create ticket error:', error);

          this.errorMessage =
            error.error?.Message ||
            error.error?.message ||
            'Unable to create ticket. Please try again.';
        }
      });
  }

  private completeTicketCreation(): void {
    this.isSubmitting = false;
    this.successMessage = this.selectedFile
      ? 'Ticket and attachment created successfully!'
      : 'Ticket created successfully!';

    setTimeout(() => {
      const roles = this.auth.getRoles();
      this.router.navigate([
        roles.includes('User') ? '/user/dashboard' : '/admin/tickets'
      ]);
    }, 1000);
  }
}
