import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { finalize } from 'rxjs';
import { ApiService } from '../../../../core/http/services/api.service';
import { JwtService } from '../../../../core/auth/services/jwt.service';
import { LoginRequest, LoginResponse } from '../../models/auth.models';
import { extractIdentityErrorMessage } from '../../utils/identity-error';
import { Router } from '@angular/router';

@Component({
  selector: 'app-login',
  templateUrl: './login.html',
  styleUrls: ['../../auth-form.css', './login.css'],
  imports: [ReactiveFormsModule],
})
export class Login {
  private api = inject(ApiService);
  private router = inject(Router);
  private jwt = inject(JwtService);
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

    this.api
      .post<LoginRequest, LoginResponse>('/login', {
        email: this.loginForm.value.email!,
        password: this.loginForm.value.password!,
      })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (response) => {
          this.jwt.saveToken(response.accessToken);
          this.router.navigate(['/dashboard']);
        },
        error: (err: HttpErrorResponse) => {
          console.error(err);
          this.errorMessage.set(
            err.status === 401 ? 'Invalid email or password.' : extractIdentityErrorMessage(err, 'Login failed'),
          );
        },
      });
  }
}
