import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../../core/auth/services/auth.service';
import { RegisterRequest } from '../../../../core/auth/models/auth.models';
import { extractIdentityErrorMessage } from '../../utils/identity-error';

@Component({
  selector: 'app-register',
  templateUrl: './register.html',
  styleUrls: ['../../auth-form.css', './register.css'],
  imports: [ReactiveFormsModule],
})
export class Register {
  private auth = inject(AuthService);
  private router = inject(Router);
  protected registerForm = new FormGroup({
    email: new FormControl('', [Validators.required, Validators.email]),
    password: new FormControl('', [
      Validators.required,
      Validators.minLength(6),
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).*$/),
    ]),
  });
  protected errorMessage = signal<string | null>(null);
  protected isSubmitting = signal(false);

  protected onSubmit() {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const credentials: RegisterRequest = {
      email: this.registerForm.value.email!,
      password: this.registerForm.value.password!,
    };

    this.auth
      .register(credentials)
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.router.navigate(['/dashboard']);
        },
        error: (err: HttpErrorResponse) => {
          this.errorMessage.set(extractIdentityErrorMessage(err, 'Registration failed'));
        },
      });
  }
}
