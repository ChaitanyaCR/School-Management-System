import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppShell } from './app-shell';

function renderShell() {
  const rootRoute = createRootRoute({ component: () => <AppShell>content</AppShell> });
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

function mockApi(organisation: Response) {
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) =>
      Promise.resolve(
        url.endsWith('/system/info')
          ? Response.json({ productName: 'SchoolOS', version: '0.1.0', environment: 'Testing' })
          : organisation,
      ),
    ),
  );
}

describe('AppShell', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the organisation name on an organisation subdomain', async () => {
    mockApi(Response.json({ slug: 'demo', name: 'Demo School Group' }));

    renderShell();

    expect(await screen.findByText('Demo School Group')).toBeInTheDocument();
    expect(await screen.findByText('SchoolOS')).toBeInTheDocument();
  });

  it('shows the product name when the address has no organisation', async () => {
    mockApi(
      Response.json(
        { status: 404, code: 'tenant.none' },
        { status: 404, headers: { 'content-type': 'application/problem+json' } },
      ),
    );

    renderShell();

    expect(await screen.findByText('SchoolOS')).toBeInTheDocument();
    expect(screen.queryByText('Demo School Group')).not.toBeInTheDocument();
  });
});
