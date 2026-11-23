
import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TicketService, Ticket } from '../../../services/ticket';
import * as XLSX from 'xlsx';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import { RoleBackButton } from '../../../shared/role-back-button/role-back-button';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, RoleBackButton],
  templateUrl: './reports.html',
  styleUrl: './reports.css'
})
export class Reports implements OnInit {
  tickets: Ticket[] = [];
  isLoading = true;
  errorMessage = '';

  selectedStatus = 'All';
  fromDate = '';
  toDate = '';

  constructor(
    private ticketService: TicketService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadTickets();
  }

  loadTickets(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.ticketService.getAllTickets().subscribe({
      next: response => {
        console.log('Reports API Response:', response);

        this.tickets = response || [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Reports loading error:', error);

        this.errorMessage = 'Unable to load reports. Please try again.';
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get filteredTickets(): Ticket[] {
    return this.tickets.filter(ticket => {
      const matchesStatus =
        this.selectedStatus === 'All' ||
        ticket.status?.toLowerCase() === this.selectedStatus.toLowerCase();

      const ticketDate = new Date(ticket.createdAt);

      const startDate = this.fromDate
        ? new Date(`${this.fromDate}T00:00:00`)
        : null;

      const endDate = this.toDate
        ? new Date(`${this.toDate}T23:59:59`)
        : null;

      return (
        matchesStatus &&
        (!startDate || ticketDate >= startDate) &&
        (!endDate || ticketDate <= endDate)
      );
    }).sort((a, b) =>
      new Date(b.createdAt).getTime() -
      new Date(a.createdAt).getTime()
    );
  }

  get totalTickets(): number {
    return this.filteredTickets.length;
  }

  countStatus(status: string): number {
    return this.filteredTickets.filter(
      ticket => ticket.status?.toLowerCase() === status.toLowerCase()
    ).length;
  }

  get openTickets(): number {
    return this.countStatus('Open');
  }

  get inProgressTickets(): number {
    return this.countStatus('In Progress');
  }

  get resolvedTickets(): number {
    return this.countStatus('Resolved');
  }

  get closedTickets(): number {
    return this.countStatus('Closed');
  }

  resetFilters(): void {
    this.selectedStatus = 'All';
    this.fromDate = '';
    this.toDate = '';
  }

  exportExcel(): void {
    if (!this.filteredTickets.length) {
      alert('No tickets available to export.');
      return;
    }

    const data = this.filteredTickets.map(ticket => ({
      'Ticket ID': ticket.id,
      'Title': ticket.title,
      'Description': ticket.description,
      'Category': ticket.categoryName,
      'Priority': ticket.priorityName,
      'Status': ticket.status,
      'Created Date': new Date(ticket.createdAt).toLocaleString(),
      'Updated Date': ticket.updatedAt
        ? new Date(ticket.updatedAt).toLocaleString()
        : 'Not updated'
    }));

    const worksheet = XLSX.utils.json_to_sheet(data);

    worksheet['!cols'] = [
      { wch: 12 }, { wch: 30 }, { wch: 45 }, { wch: 20 },
      { wch: 15 }, { wch: 18 }, { wch: 24 }, { wch: 24 }
    ];

    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, 'Ticket Reports');

    XLSX.writeFile(
      workbook,
      `IT-Service-Desk-Report-${new Date().toISOString().slice(0, 10)}.xlsx`
    );
  }

  exportPDF(): void {
    if (!this.filteredTickets.length) {
      alert('No tickets available to export.');
      return;
    }

    const doc = new jsPDF();

    doc.setFontSize(18);
    doc.text('IT Service Desk - Ticket Report', 14, 18);

    doc.setFontSize(10);
    doc.text(`Generated: ${new Date().toLocaleString()}`, 14, 26);
    doc.text(`Total Tickets: ${this.totalTickets}`, 14, 34);
    doc.text(`Open: ${this.openTickets}`, 14, 41);
    doc.text(`In Progress: ${this.inProgressTickets}`, 65, 41);
    doc.text(`Resolved: ${this.resolvedTickets}`, 125, 41);
    doc.text(`Closed: ${this.closedTickets}`, 14, 48);

    autoTable(doc, {
      startY: 56,
      head: [['ID', 'Title', 'Category', 'Priority', 'Status', 'Created']],
      body: this.filteredTickets.map(ticket => [
        ticket.id,
        ticket.title,
        ticket.categoryName,
        ticket.priorityName,
        ticket.status,
        new Date(ticket.createdAt).toLocaleDateString()
      ]),
      styles: { fontSize: 8, cellPadding: 3, overflow: 'linebreak' },
      headStyles: { fillColor: [35, 55, 90] }
    });

    doc.save(
      `IT-Service-Desk-Report-${new Date().toISOString().slice(0, 10)}.pdf`
    );
  }
}
