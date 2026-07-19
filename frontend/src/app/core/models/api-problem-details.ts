import { HttpErrorResponse } from '@angular/common/http';

export interface ApiProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export class ApiValidationError extends Error {
  constructor(readonly problem: ApiProblemDetails) {
    super(problem.title ?? 'Request validation failed.');
    this.name = 'ApiValidationError';
  }
}

export function toApiValidationError(error: unknown): unknown {
  if (error instanceof HttpErrorResponse && error.status === 400 && isApiProblemDetails(error.error)) {
    return new ApiValidationError(error.error);
  }

  return error;
}

function isApiProblemDetails(value: unknown): value is ApiProblemDetails {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const problem = value as Record<string, unknown>;
  return (
    isOptionalString(problem['type']) &&
    isOptionalString(problem['title']) &&
    isOptionalNumber(problem['status']) &&
    isOptionalString(problem['detail']) &&
    isOptionalString(problem['instance']) &&
    isOptionalValidationErrors(problem['errors']) &&
    (typeof problem['title'] === 'string' ||
      typeof problem['detail'] === 'string' ||
      typeof problem['status'] === 'number' ||
      problem['errors'] !== undefined)
  );
}

function isOptionalString(value: unknown): boolean {
  return value === undefined || typeof value === 'string';
}

function isOptionalNumber(value: unknown): boolean {
  return value === undefined || typeof value === 'number';
}

function isOptionalValidationErrors(value: unknown): boolean {
  if (value === undefined) {
    return true;
  }

  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return false;
  }

  return Object.values(value).every(
    (messages) => Array.isArray(messages) && messages.every((message) => typeof message === 'string'),
  );
}
