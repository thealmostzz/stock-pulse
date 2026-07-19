import { Observable, throwError } from 'rxjs';

import { toApiValidationError } from '../models/api-problem-details';

export function rethrowApiError<T>(error: unknown): Observable<T> {
  return throwError(() => toApiValidationError(error));
}
