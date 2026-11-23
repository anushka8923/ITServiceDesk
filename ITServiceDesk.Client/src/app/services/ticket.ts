import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Ticket {
  id: number;
  title: string;
  description: string;
  status: string;
  createdAt: string;
  updatedAt?: string;
  categoryId: number;
  categoryName: string;
  priorityId: number;
  priorityName: string;
  createdByUserId: string;
}

export interface UpdateTicketRequest {
  title: string;
  description: string;
  categoryId: number;
  priorityId: number;
  status: string;
}

export interface TicketAttachment {
  id: number;
  fileName: string;
  contentType: string;
  uploadedAt: string;
  ticketId: number;
  uploadedByUserId: string;
}

@Injectable({ providedIn: 'root' })
export class TicketService {
  private apiUrl = 'http://localhost:5204/api/Tickets';
  private baseUrl = 'http://localhost:5204/api/tickets';

  constructor(private http: HttpClient) {}

  getAllTickets(): Observable<Ticket[]> {
    return this.http.get<Ticket[]>(this.apiUrl);
  }

  searchTickets(search?: string, status?: string): Observable<Ticket[]> {
    let params = new HttpParams();

    if (search) params = params.set('search', search);
    if (status && status !== 'All') {
      params = params.set('status', status);
    }

    return this.http.get<Ticket[]>(`${this.apiUrl}/search`, { params });
  }

  updateTicket(
    id: number,
    ticket: UpdateTicketRequest
  ): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(
      `${this.apiUrl}/${id}`,
      ticket
    );
  }

  deleteTicket(id: number): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.apiUrl}/${id}`
    );
  }

  getAttachments(ticketId: number): Observable<TicketAttachment[]> {
    return this.http.get<TicketAttachment[]>(
      `${this.baseUrl}/${ticketId}/attachments`
    );
  }

  uploadAttachment(
    ticketId: number,
    file: File
  ): Observable<TicketAttachment> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<TicketAttachment>(
      `${this.baseUrl}/${ticketId}/attachments`,
      formData
    );
  }

  downloadAttachment(
    ticketId: number,
    attachmentId: number
  ): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl}/${ticketId}/attachments/${attachmentId}/download`,
      { responseType: 'blob' }
    );
  }
}
