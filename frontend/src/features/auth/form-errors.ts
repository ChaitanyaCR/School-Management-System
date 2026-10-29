import { ApiError } from '@/api/http';

const GENERIC_ERROR = 'Something went wrong. Check your connection and try again.';

/** The message to show for a failed request; the API's titles are written for end users. */
export function errorMessage(error: unknown): string {
  return error instanceof ApiError ? (error.problem?.title ?? GENERIC_ERROR) : GENERIC_ERROR;
}

/** Messages for one input, from a validation problem's `errors` (e.g. password rules). */
export function fieldErrors(error: unknown, field: string): string[] {
  return error instanceof ApiError ? (error.problem?.errors?.[field] ?? []) : [];
}

export const PASSWORD_HINT =
  'At least 10 characters. A few unrelated words make a strong, memorable password.';
