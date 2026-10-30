/** RFC 9457 problem response returned by the API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  /** Stable, machine-readable error code (e.g. "student.not_found"). */
  code?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? `Request failed with status ${String(status)}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  get code(): string | undefined {
    return this.problem?.code;
  }
}

/** The API sets this readable cookie; its value must be echoed in the header below (CSRF protection). */
export const ANTIFORGERY_COOKIE = 'XSRF-TOKEN';
export const ANTIFORGERY_HEADER = 'X-XSRF-TOKEN';

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

function readCookie(name: string): string | undefined {
  const prefix = `${name}=`;
  const cookie = document.cookie.split('; ').find((c) => c.startsWith(prefix));
  return cookie === undefined ? undefined : decodeURIComponent(cookie.slice(prefix.length));
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('json')) {
    return null;
  }
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

/**
 * Fetch wrapper used by every generated API hook. Requests are same-origin (cookies are sent
 * automatically) and state-changing ones carry the antiforgery token; non-2xx responses throw
 * {@link ApiError}.
 */
export async function http<T>(url: string, init?: RequestInit): Promise<T> {
  // HeadersInit may be an object, an array of pairs or a Headers instance; Headers normalises all three.
  const headers = new Headers(init?.headers);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  const method = (init?.method ?? 'GET').toUpperCase();
  const token = SAFE_METHODS.has(method) ? undefined : readCookie(ANTIFORGERY_COOKIE);
  if (token !== undefined) {
    headers.set(ANTIFORGERY_HEADER, token);
  }

  const response = await fetch(url, { ...init, credentials: 'same-origin', headers });

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  // 204, and 202 without a body, have nothing to parse.
  const body = response.status === 204 ? '' : await response.text();
  return (body === '' ? undefined : JSON.parse(body)) as T;
}
