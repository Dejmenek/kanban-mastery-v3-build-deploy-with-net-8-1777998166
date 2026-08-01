import { HttpErrorResponse } from '@angular/common/http';

interface IdentityProblemDetails {
  title?: string;
  errors?: Record<string, string[]>;
}

export function extractIdentityErrorMessage(error: HttpErrorResponse, fallback: string): string {
  const body: IdentityProblemDetails | undefined = error.error;
  const firstFieldError = body?.errors ? Object.values(body.errors)[0]?.[0] : undefined;
  return firstFieldError ?? body?.title ?? fallback;
}
