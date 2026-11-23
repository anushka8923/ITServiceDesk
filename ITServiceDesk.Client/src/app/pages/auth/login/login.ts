
import { ChangeDetectorRef, Component } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Auth } from '../../../services/auth';
import { AuthErrorPopup } from '../../../shared/auth-error-popup/auth-error-popup';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink, AuthErrorPopup],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {

  email = '';
  password = '';
  errorMessage = '';
  isLoading = false;
  registrationSuccessMessage = '';

  constructor(
    private auth: Auth,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    this.registrationSuccessMessage =
      this.router.getCurrentNavigation()?.extras.state?.['registrationSuccessMessage'] || '';
  }

  onLogin(form: NgForm): void {
    if (this.isLoading) return;

    this.errorMessage = '';
    const email = this.email.trim();

    if (!email) {
      this.errorMessage = 'Enter your email.';
      return;
    }
    if (form.controls['email']?.errors?.['email']) {
      this.errorMessage = 'Enter a valid email.';
      return;
    }
    if (!this.password) {
      this.errorMessage = 'Enter your password.';
      return;
    }

    this.isLoading = true;

    this.auth.login({
      email,
      password: this.password
    }).subscribe({
      next: (response) => {
        this.isLoading = false;

        if (response.roles.includes('SuperAdmin')) {
          this.router.navigate(['/superadmin/dashboard']);
        } else if (response.roles.includes('Admin')) {
          this.router.navigate(['/admin/dashboard']);
        } else {
          this.router.navigate(['/user/dashboard']);
        }
      },
    error: (error) => {
        this.isLoading = false;
        this.errorMessage = this.getLoginError(error);
        this.cdr.markForCheck();
      }
    });
  }

  dismissError(): void {
    this.errorMessage = '';
  }

  private getLoginError(error: any): string {
    const apiMessage = error.error?.Message || error.error?.message || '';
    if (error.status === 401) {
      return apiMessage || 'Invalid email or password.';
    }

    const apiErrors = error.error?.Errors ?? error.error?.errors;
    const details: string[] = Array.isArray(apiErrors)
      ? apiErrors.map(String)
      : apiErrors && typeof apiErrors === 'object'
        ? Object.values(apiErrors).flat().map(String)
        : [];
    const detailText = details.join(' ');

    if (/email/i.test(detailText)) {
      return 'Enter a valid email.';
    }
    if (/password/i.test(detailText)) {
      return 'Enter your password.';
    }
    if (error.status === 0) {
      return 'Unable to connect to the server. Please try again.';
    }

    return apiMessage || details[0] || error.error?.title ||
      'Unable to sign in. Please try again.';
  }
}
