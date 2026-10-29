import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router';
import { render } from '@testing-library/react';
import { vi } from 'vitest';
import { routeTree } from '@/routeTree.gen';

/** A fake API: `"METHOD /path"` → response (or a function building one from the request body). */
export type FakeApi = Record<string, Response | ((body: unknown) => Response)>;

export const problem = (status: number, body: Record<string, unknown>) =>
  Response.json(
    { status, ...body },
    { status, headers: { 'content-type': 'application/problem+json' } },
  );

/** Defaults: an organisation address with nobody signed in. */
const defaults: FakeApi = {
  'GET /api/v1/system/info': Response.json({
    productName: 'SchoolOS',
    version: '0.1.0',
    environment: 'Testing',
  }),
  'GET /api/v1/tenancy/current': Response.json({ slug: 'demo', name: 'Demo School Group' }),
  'GET /api/v1/identity/session': Response.json({ user: null }),
};

/** Stubs fetch with the fake API; returns the mock so tests can inspect requests. */
export function stubApi(api: FakeApi) {
  const routes = { ...defaults, ...api };
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${url}`;
    const route = routes[key];
    if (route === undefined) {
      return Promise.resolve(problem(404, { title: `No fake for ${key}` }));
    }
    const body: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined;
    // Responses can be read once; clone so a route may answer repeatedly.
    return Promise.resolve(typeof route === 'function' ? route(body) : route.clone());
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** Renders the whole app (real routes, guards and pages) at `path`. */
export function renderApp(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createRouter({
    routeTree,
    context: { queryClient },
    history: createMemoryHistory({ initialEntries: [path] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { router, queryClient };
}
