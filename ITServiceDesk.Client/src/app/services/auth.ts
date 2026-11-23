
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  email: string;
  fullName: string;
  roles: string[];
  expiresAt: string;
}

export interface ManagedUser {
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
  emailConfirmed: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class Auth {

  private apiUrl = 'http://localhost:5204/api/Auth';

  constructor(private http: HttpClient) {}

  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(
      `${this.apiUrl}/login`,
      credentials
    ).pipe(
      tap(response => {
        localStorage.setItem('token', response.token);
        localStorage.setItem('email', response.email);
        localStorage.setItem('fullName', response.fullName);
        localStorage.setItem('roles', JSON.stringify(response.roles));
        localStorage.setItem('expiresAt', response.expiresAt);
      })
    );
  }

  register(details: RegisterRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.apiUrl}/register`, details);
  }

  getUsers(): Observable<ManagedUser[]> {
    return this.http.get<ManagedUser[]>(`${this.apiUrl}/users`);
  }

  updateUserRole(userId: string, role: 'Admin' | 'User'):
    Observable<{ message: string; userId: string; email: string; role: string }> {
    return this.http.put<{ message: string; userId: string; email: string; role: string }>(
      `${this.apiUrl}/users/${encodeURIComponent(userId)}/role`,
      { role }
    );
  }

  getToken(): string | null {
    return localStorage.getItem('token');
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  getRoles(): string[] {
    return JSON.parse(localStorage.getItem('roles') || '[]');
  }

  logout(): void {
    localStorage.clear();
  }
}
