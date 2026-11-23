
import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RoleBackButton } from '../../../shared/role-back-button/role-back-button';

interface Category {
  id: number;
  name: string;
  description?: string;
  isActive: boolean;
}

@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, RoleBackButton],
  templateUrl: './categories.html',
  styleUrl: './categories.css'
})
export class Categories implements OnInit {
  private apiUrl = 'http://localhost:5204/api/Categories';

  categories: Category[] = [];
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  searchText = '';
  statusFilter = 'All';

  showModal = false;
  isEditMode = false;
  isSaving = false;

  selectedId: number | null = null;
  categoryName = '';
  categoryDescription = '';
  categoryActive = true;
  formError = '';

  constructor(
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadCategories();
  }

  loadCategories(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.http.get<Category[]>(this.apiUrl).subscribe({
      next: response => {
        this.categories = response || [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Category loading error:', error);
        this.errorMessage = 'Unable to load categories.';
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get filteredCategories(): Category[] {
    return this.categories.filter(category => {
      const search = this.searchText.toLowerCase().trim();

      const matchesSearch =
        !search ||
        category.name.toLowerCase().includes(search) ||
        (category.description || '').toLowerCase().includes(search);

      const matchesStatus =
        this.statusFilter === 'All' ||
        (this.statusFilter === 'Active' && category.isActive) ||
        (this.statusFilter === 'Inactive' && !category.isActive);

      return matchesSearch && matchesStatus;
    });
  }

  get activeCount(): number {
    return this.categories.filter(c => c.isActive).length;
  }

  get inactiveCount(): number {
    return this.categories.filter(c => !c.isActive).length;
  }

  openAddModal(): void {
    this.isEditMode = false;
    this.selectedId = null;
    this.categoryName = '';
    this.categoryDescription = '';
    this.categoryActive = true;
    this.formError = '';
    this.showModal = true;
  }

  openEditModal(category: Category): void {
    this.isEditMode = true;
    this.selectedId = category.id;
    this.categoryName = category.name;
    this.categoryDescription = category.description || '';
    this.categoryActive = category.isActive;
    this.formError = '';
    this.showModal = true;
  }

  closeModal(): void {
    if (this.isSaving) return;
    this.showModal = false;
  }

  saveCategory(): void {
    this.formError = '';
    this.successMessage = '';

    const name = this.categoryName.trim();
    const description = this.categoryDescription.trim();

    if (name.length < 2) {
      this.formError = 'Category name must contain at least 2 characters.';
      return;
    }

    if (name.length > 100) {
      this.formError = 'Category name cannot exceed 100 characters.';
      return;
    }

    if (description.length > 500) {
      this.formError = 'Description cannot exceed 500 characters.';
      return;
    }

    const payload = {
      name,
      description: description || null,
      isActive: this.categoryActive
    };

    this.isSaving = true;

    const request = this.isEditMode && this.selectedId !== null
      ? this.http.put<Category>(
          `${this.apiUrl}/${this.selectedId}`,
          payload
        )
      : this.http.post<Category>(this.apiUrl, {
          name,
          description: description || null
        });

    request.subscribe({
      next: () => {
        this.isSaving = false;
        this.showModal = false;
        this.successMessage = this.isEditMode
          ? 'Category updated successfully.'
          : 'Category added successfully.';

        this.loadCategories();
        this.cdr.detectChanges();
      },
      error: error => {
        console.error('Save category error:', error);

        this.formError =
          error.status === 409
            ? 'A category with this name already exists.'
            : error.status === 400
              ? 'Please check the entered category details.'
              : 'Unable to save category. Please try again.';

        this.isSaving = false;
        this.cdr.detectChanges();
      }
    });
  }

  toggleStatus(category: Category): void {
    const updatedCategory = {
      name: category.name,
      description: category.description || null,
      isActive: !category.isActive
    };

    this.http.put<Category>(
      `${this.apiUrl}/${category.id}`,
      updatedCategory
    ).subscribe({
      next: () => {
        this.successMessage = updatedCategory.isActive
          ? 'Category activated successfully.'
          : 'Category deactivated successfully.';

        this.loadCategories();
      },
      error: error => {
        console.error('Category status update error:', error);
        this.errorMessage = 'Unable to update category status.';
        this.cdr.detectChanges();
      }
    });
  }

  deleteCategory(category: Category): void {
    const confirmed = confirm(
      `Are you sure you want to deactivate "${category.name}"?`
    );

    if (!confirmed) return;

    this.http.delete(`${this.apiUrl}/${category.id}`).subscribe({
      next: () => {
        this.successMessage = 'Category deactivated successfully.';
        this.loadCategories();
      },
      error: error => {
        console.error('Delete category error:', error);
        this.errorMessage = 'Unable to deactivate category.';
        this.cdr.detectChanges();
      }
    });
  }

  clearMessages(): void {
    this.successMessage = '';
    this.errorMessage = '';
  }
}
