import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRouter, RouterProvider } from '@tanstack/react-router';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { getGetSessionQueryKey } from './api/generated/endpoints';
import { ApiError } from './api/http';
import { setSession } from './features/auth/session';
import { routeTree } from './routeTree.gen';
import './index.css';

/** Reacts to responses that mean the session is gone or the antiforgery token is stale. */
function handleSessionErrors(error: unknown) {
  if (!(error instanceof ApiError)) {
    return;
  }
  // The session ended (expired, password changed, signed out elsewhere): back to sign-in.
  if (error.status === 401 && error.code !== 'identity.invalid_credentials') {
    setSession(queryClient, { user: null });
    void router.invalidate();
  }
  // The token belongs to an earlier session; reloading the session issues a fresh one.
  if (error.code === 'antiforgery.invalid') {
    void queryClient.invalidateQueries({ queryKey: getGetSessionQueryKey() });
  }
}

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: handleSessionErrors }),
  mutationCache: new MutationCache({ onError: handleSessionErrors }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Retry once on network and server errors; a 4xx answer will not change on retry.
      retry: (failureCount, error) =>
        failureCount < 1 && !(error instanceof ApiError && error.status < 500),
    },
  },
});

const router = createRouter({
  routeTree,
  context: { queryClient },
  defaultPreload: 'intent',
});

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error('Root element #root not found');
}

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
