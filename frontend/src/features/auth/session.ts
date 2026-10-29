import type { QueryClient } from '@tanstack/react-query';
import {
  getGetSessionQueryKey,
  getGetSessionQueryOptions,
  useGetSession,
} from '@/api/generated/endpoints';
import type { CurrentUserResponse, SessionResponse } from '@/api/generated/model';

/**
 * The session, from cache when available (the cache is updated on sign-in, sign-out and expiry).
 * Loading it also issues the antiforgery token.
 */
export function loadSession(queryClient: QueryClient): Promise<SessionResponse> {
  return queryClient.query({ ...getGetSessionQueryOptions(), staleTime: 'static' });
}

export function setSession(queryClient: QueryClient, session: SessionResponse) {
  queryClient.setQueryData(getGetSessionQueryKey(), session);
}

export function useCurrentUser(): CurrentUserResponse | null {
  return useGetSession().data?.user ?? null;
}

/**
 * Where to go after signing in. Only paths on this site are allowed, so a crafted link
 * cannot send someone to another website once they have signed in.
 */
export function safeRedirect(target: string | undefined): string {
  return target?.startsWith('/') && !target.startsWith('//') && !target.startsWith('/\\')
    ? target
    : '/';
}

/** Reads `#uid=…&token=…` from an emailed link; the fragment never reaches any server. */
export function readLinkFragment(hash: string): { userId: string; token: string } | null {
  const params = new URLSearchParams(hash.replace(/^#/, ''));
  const userId = params.get('uid');
  const token = params.get('token');
  return userId && token ? { userId, token } : null;
}
