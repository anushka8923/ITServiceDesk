import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-auth-error-popup',
  standalone: true,
  template: `
    @if (message) {
      <div class="popup-backdrop">
        <section class="error-popup" role="alertdialog" aria-modal="true" aria-labelledby="auth-error-title" aria-describedby="auth-error-message">
          <div class="error-icon" aria-hidden="true">!</div>
          <h2 id="auth-error-title">Please check your details</h2>
          <p id="auth-error-message">{{ message }}</p>
          <button type="button" (click)="closed.emit()">OK</button>
        </section>
      </div>
    }
  `,
  styles: [`
    .popup-backdrop {
      position: fixed;
      inset: 0;
      z-index: 2000;
      display: grid;
      place-items: center;
      padding: 1rem;
      background: rgba(18, 29, 48, 0.48);
    }

    .error-popup {
      width: min(100%, 390px);
      padding: 1.5rem;
      border: 1px solid #e7eaf0;
      border-radius: 14px;
      background: #fff;
      box-shadow: 0 18px 55px rgba(20, 35, 60, 0.2);
      text-align: center;
    }

    .error-icon {
      display: grid;
      width: 42px;
      height: 42px;
      margin: 0 auto 0.85rem;
      place-items: center;
      border-radius: 50%;
      background: #fff0f0;
      color: #c83939;
      font-size: 1.45rem;
      font-weight: 700;
    }

    h2 {
      margin: 0 0 0.5rem;
      color: #243047;
      font-size: 1.1rem;
      font-weight: 700;
    }

    p {
      margin: 0;
      color: #667085;
      font-size: 0.95rem;
      line-height: 1.5;
      overflow-wrap: anywhere;
    }

    button {
      min-width: 82px;
      margin-top: 1.25rem;
      padding: 0.55rem 1rem;
      border: 0;
      border-radius: 7px;
      background: #3978f6;
      color: #fff;
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }

    button:hover { background: #2864df; }
    button:focus-visible { outline: 3px solid rgba(57, 120, 246, 0.35); outline-offset: 2px; }
  `]
})
export class AuthErrorPopup {
  @Input() message = '';
  @Output() closed = new EventEmitter<void>();
}
