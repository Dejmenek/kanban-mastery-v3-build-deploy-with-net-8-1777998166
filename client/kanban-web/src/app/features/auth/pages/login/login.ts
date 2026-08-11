import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/services/auth.service';
import { LoginRequest } from '../../../../core/auth/models/auth.models';
import { extractIdentityErrorMessage } from '../../utils/identity-error';
import { Router } from '@angular/router';
import { ErrorMessage } from '../../../../shared/components/error-message/error-message';

@Component({
  selector: 'app-login',
  templateUrl: './login.html',
  styleUrls: ['../../auth-form.css', './login.css'],
  imports: [ReactiveFormsModule, ErrorMessage],
})
export class Login {
  private auth = inject(AuthService);
  private router = inject(Router);
  protected loginForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [Validators.required]),
  });
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);

  protected onSubmit() {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const credentials: LoginRequest = {
      email: this.loginForm.value.email!,
      password: this.loginForm.value.password!,
    };

    this.auth
      .login(credentials)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.router.navigate(['/dashboard']);
        },
        error: (err: HttpErrorResponse) => {
          this.errorMessage.set(
            err.status === 401 ? 'Invalid email or password.' : extractIdentityErrorMessage(err, 'Login failed'),
          );
        },
      });
  }
}
