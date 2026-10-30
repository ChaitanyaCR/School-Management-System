import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, http } from './http';

describe('http', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns the parsed JSON body on success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ ok: true })));

    await expect(http<{ ok: boolean }>('/api/v1/x')).resolves.toEqual({ ok: true });
  });

  it('throws ApiError carrying the problem details on failure', async () => {
    const problem = { title: 'Student not found.', status: 404, code: 'student.not_found' };
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        Response.json(problem, {
          status: 404,
          headers: { 'content-type': 'application/problem+json' },
        }),
      ),
    );

    const error = await http('/api/v1/x').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(404);
    expect((error as ApiError).problem?.code).toBe('student.not_found');
    expect((error as ApiError).message).toBe('Student not found.');
  });

  it('returns undefined for 204 No Content and for 202 without a body', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 204 })));
    await expect(http('/api/v1/x')).resolves.toBeUndefined();

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 202 })));
    await expect(http('/api/v1/x', { method: 'POST' })).resolves.toBeUndefined();
  });

  describe('antiforgery token', () => {
    afterEach(() => {
      document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
    });

    function sentHeaders(fetchMock: ReturnType<typeof vi.fn>): Headers {
      const init = fetchMock.mock.calls[0]?.[1] as RequestInit;
      return init.headers as Headers;
    }

    it('is echoed from the cookie on state-changing requests', async () => {
      document.cookie = 'XSRF-TOKEN=CfDJ8-token_value; path=/';
      const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
      vi.stubGlobal('fetch', fetchMock);

      await http('/api/v1/x', { method: 'POST' });

      expect(sentHeaders(fetchMock).get('X-XSRF-TOKEN')).toBe('CfDJ8-token_value');
    });

    it('is not sent on reads', async () => {
      document.cookie = 'XSRF-TOKEN=CfDJ8-token_value; path=/';
      const fetchMock = vi.fn().mockResolvedValue(Response.json({}));
      vi.stubGlobal('fetch', fetchMock);

      await http('/api/v1/x');

      expect(sentHeaders(fetchMock).has('X-XSRF-TOKEN')).toBe(false);
    });
  });
});
