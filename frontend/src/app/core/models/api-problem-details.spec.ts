import { HttpErrorResponse } from '@angular/common/http';

import { ApiValidationError, toApiValidationError } from './api-problem-details';

describe('toApiValidationError', () => {
  for (const errors of [null, ['must be an object'], { ticker: ['must be a string', 1] }]) {
    it(`preserves an invalid errors payload: ${JSON.stringify(errors)}`, () => {
      const response = new HttpErrorResponse({
        error: { title: 'Validation failed.', status: 400, errors },
        status: 400,
        statusText: 'Bad Request',
      });

      expect(toApiValidationError(response)).toBe(response);
    });
  }

  it('maps a record of string arrays to ApiValidationError', () => {
    const response = new HttpErrorResponse({
      error: { title: 'Validation failed.', status: 400, errors: { ticker: ['is required'] } },
      status: 400,
      statusText: 'Bad Request',
    });

    expect(toApiValidationError(response)).toEqual(jasmine.any(ApiValidationError));
  });
});
