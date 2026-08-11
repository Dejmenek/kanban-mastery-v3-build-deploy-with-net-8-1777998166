import { HttpErrorResponse } from '@angular/common/http';

export function extractErrorMessage(err: HttpErrorResponse, fallback: string): string {
  if (typeof err.error === 'string' && err.error.length > 0) return err.error;
  return fallback;
}
