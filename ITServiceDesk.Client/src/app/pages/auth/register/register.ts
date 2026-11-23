import { ChangeDetectorRef, Component } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { Auth } from '../../../services/auth';
import { AuthErrorPopup } from '../../../shared/auth-error-popup/auth-error-popup';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthErrorPopup],
  templateUrl: './register.html',
  styleUrl: './register.css'
})
export class Register {
  fullName = '';
  email = '';
  password = '';
  confirmPassword = '';
  showPassword = false;
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  constructor(
    private auth: Auth,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  onRegister(form: NgForm): void {
    if (this.isLoading) return;

    this.errorMessage = '';
    this.successMessage = '';

    const fullName = this.fullName.trim();
    const email = this.email.trim();

    if (!fullName) {
      this.errorMessage = 'Full Name is required.';
      return;
    }
    if (fullName.length < 2 || fullName.length > 100) {
      this.errorMessage = 'Full Name must be between 2 and 100 characters.';
      return;
    }
    if (!email) {
      this.errorMessage = 'Email is required.';
      return;
    }
    if (form.controls['email']?.errors?.['email']) {
      this.errorMessage = 'Enter a valid email.';
      return;
    }
    if (!this.password) {
      this.errorMessage = 'Password is required.';
      return;
    }

    const passwordMeetsIdentityRequirements =
      this.password.length >= 8 &&
      this.password.length <= 100 &&
      /\p{Nd}/u.test(this.password) &&
      /\p{Ll}/u.test(this.password) &&
      /\p{Lu}/u.test(this.password) &&
      /[^\p{L}\p{Nd}]/u.test(this.password);

    if (!passwordMeetsIdentityRequirements) {
      this.errorMessage = 'Password does not meet the requirements.';
      return;
    }
    if (!this.confirmPassword) {
      this.errorMessage = 'Confirm your password.';
      return;
    }
    if (this.password !== this.confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.isLoading = true;
    this.auth.register({
      fullName,
      email,
      password: this.password
    }).pipe(
      finalize(() => this.isLoading = false)
    ).subscribe({
      next: () => {
        this.isLoading = false;
        this.successMessage = 'Registration successful! Please login with your credentials.';
        void this.router.navigate(['/login'], {
          state: { registrationSuccessMessage: this.successMessage }
        });
      },
      error: error => {
        this.isLoading = false;
        this.errorMessage = this.getRegistrationError(error);
        this.cdr.markForCheck();
      }
    });
  }

  dismissError(): void {
    this.errorMessage = '';
  }

  private getRegistrationError(error: any): string {
    const apiMessage = error.error?.Message || error.error?.message || '';
    const lowerMessage = String(apiMessage).toLowerCase();

    if (error.status === 409 || lowerMessage.includes('email is already registered')) {
      return 'Email is already registered.';
    }
    if (lowerMessage.includes('demo admin/superadmin')) {
      return 'This demo account cannot be registered. Please use the Login page.';
    }

    const apiErrors = error.error?.Errors ?? error.error?.errors;
    const details: string[] = Array.isArray(apiErrors)
      ? apiErrors.map(String)
      : apiErrors && typeof apiErrors === 'object'
        ? Object.values(apiErrors).flat().map(String)
        : [];
    const detailText = details.join(' ');

    if (/password/i.test(detailText)) {
      return 'Password does not meet the requirements.';
    }
    if (/email|username/i.test(detailText) && /already|taken|registered/i.test(detailText)) {
      return 'Email is already registered.';
    }
    if (/email/i.test(detailText)) {
      return 'Enter a valid email.';
    }
    if (/fullname|full name/i.test(detailText)) {
      return 'Full Name must be between 2 and 100 characters.';
    }
    if (error.status === 0) {
      return 'Unable to connect to the server. Please try again.';
    }

    return apiMessage || details[0] || error.error?.title ||
      'Unable to register. Please try again.';
  }
}
