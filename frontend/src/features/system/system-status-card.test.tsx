import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SystemStatusCard } from './system-status-card';

function renderWithQueryClient(ui: ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

function mockFetch(response: Response) {
  const fetchMock = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('SystemStatusCard', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows version and environment when the API responds', async () => {
    const fetchMock = mockFetch(
      Response.json({ productName: 'SMS', version: '0.1.0', environment: 'Development' }),
    );

    renderWithQueryClient(<SystemStatusCard />);

    expect(await screen.findByText('Connected')).toBeInTheDocument();
    expect(screen.getByText('0.1.0')).toBeInTheDocument();
    expect(screen.getByText('Development')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/system/info', expect.anything());
  });

  it('shows an error when the API is unreachable', async () => {
    mockFetch(new Response(null, { status: 503 }));

    renderWithQueryClient(<SystemStatusCard />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot reach the API server.');
  });
});
