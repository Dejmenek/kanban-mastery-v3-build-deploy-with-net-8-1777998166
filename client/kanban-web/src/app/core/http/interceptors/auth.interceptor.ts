import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { JwtService } from '../../auth/services/jwt.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const jwt = inject(JwtService);
  const token = jwt.getToken();
  if (token) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
  return next(req);
};
