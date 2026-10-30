import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { problem, renderApp, stubApi } from '@/test/render-app';
import { readLinkFragment, safeRedirect } from './session';

const asha = { id: '0192', fullName: 'Asha Verma', email: 'admin@demo.test' };

describe('sign-in', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('sends visitors who are not signed in to the sign-in page', async () => {
    stubApi({});

    const { router } = renderApp('/');

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(await screen.findByText('Demo School Group')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.search).toEqual({ redirect: '/' });
  });

  it('signs in and returns to the requested page', async () => {
    const fetchMock = stubApi({
      'POST /api/v1/identity/session': Response.json({ user: asha }),
    });
    const { router } = renderApp('/login?redirect=%2F');

    await userEvent.type(await screen.findByLabelText('Email or mobile number'), 'admin@demo.test');
    await userEvent.type(screen.getByLabelText('Password'), 'kettle-river-lantern');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByText('Asha Verma')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/identity/session',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          identifier: 'admin@demo.test',
          password: 'kettle-river-lantern',
          rememberMe: false,
        }),
      }),
    );
  });

  it('shows the reason when sign-in fails', async () => {
    stubApi({
      'POST /api/v1/identity/session': problem(401, {
        title: 'Incorrect email, mobile number or password.',
        code: 'identity.invalid_credentials',
      }),
    });
    renderApp('/login');

    await userEvent.type(await screen.findByLabelText('Email or mobile number'), '9876543210');
    await userEvent.type(screen.getByLabelText('Password'), 'wrong-password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Incorrect email, mobile number or password.',
    );
  });

  it('explains where to sign in when the address is not a school', async () => {
    stubApi({
      'GET /api/v1/tenancy/current': problem(404, { code: 'tenant.none' }),
    });

    renderApp('/login');

    expect(await screen.findByText("Use your school's address")).toBeInTheDocument();
    expect(screen.queryByLabelText('Password')).not.toBeInTheDocument();
  });

  it('signs out and clears the session', async () => {
    let session: unknown = { user: asha };
    stubApi({
      'GET /api/v1/identity/session': () => Response.json(session),
      'DELETE /api/v1/identity/session': () => {
        session = { user: null };
        return new Response(null, { status: 204 });
      },
    });
    const { router } = renderApp('/');

    await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
  });
});

describe('invitation link', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  async function choosePassword(password: string) {
    await userEvent.type(await screen.findByLabelText('New password'), password);
    await userEvent.type(screen.getByLabelText('Confirm new password'), password);
    await userEvent.click(screen.getByRole('button', { name: 'Activate account' }));
  }

  it('shows the password rules the server applied, then activates and signs in', async () => {
    const accept = vi
      .fn<(body: unknown) => Response>()
      .mockReturnValueOnce(
        problem(400, {
          title: 'Choose a different password.',
          code: 'identity.password_rejected',
          errors: { password: ['This password has appeared in a data breach.'] },
        }),
      )
      .mockReturnValueOnce(Response.json({ user: asha }));
    stubApi({ 'POST /api/v1/identity/invitations/accept': accept });
    const { router } = renderApp('/accept-invite#uid=0192&token=abc%2Bdef');

    await choosePassword('password1234');
    expect(await screen.findByText('This password has appeared in a data breach.')).toBeVisible();

    await userEvent.clear(screen.getByLabelText('New password'));
    await userEvent.clear(screen.getByLabelText('Confirm new password'));
    await choosePassword('lantern-meadow-copper');

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/');
    });
    expect(accept).toHaveBeenLastCalledWith({
      userId: '0192',
      token: 'abc+def',
      password: 'lantern-meadow-copper',
    });
  });

  it('explains an expired or used link', async () => {
    stubApi({
      'POST /api/v1/identity/invitations/accept': problem(400, {
        title: 'This link is invalid or has expired.',
        code: 'identity.invalid_token',
      }),
    });
    renderApp('/accept-invite#uid=0192&token=used');

    await choosePassword('lantern-meadow-copper');

    expect(await screen.findByText('This link is invalid or has expired')).toBeInTheDocument();
  });

  it('does not submit when the confirmation differs', async () => {
    const fetchMock = stubApi({});
    renderApp('/accept-invite#uid=0192&token=abc');

    await userEvent.type(await screen.findByLabelText('New password'), 'lantern-meadow-copper');
    await userEvent.type(screen.getByLabelText('Confirm new password'), 'lantern-meadow-coppe');

    expect(screen.getByText('The passwords do not match.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Activate account' })).toBeDisabled();
    expect(fetchMock).not.toHaveBeenCalledWith(
      '/api/v1/identity/invitations/accept',
      expect.anything(),
    );
  });
});

describe('safeRedirect', () => {
  it.each([
    ['/students?page=2', '/students?page=2'],
    [undefined, '/'],
    ['https://evil.example', '/'],
    ['//evil.example', '/'],
    ['/\\evil.example', '/'],
    ['javascript:alert(1)', '/'],
  ])('%s → %s', (target, expected) => {
    expect(safeRedirect(target)).toBe(expected);
  });
});

describe('readLinkFragment', () => {
  it('reads the user id and decoded token', () => {
    expect(readLinkFragment('#uid=0192&token=a%2Bb%3D')).toEqual({ userId: '0192', token: 'a+b=' });
  });

  it('returns null when either is missing', () => {
    expect(readLinkFragment('#uid=0192')).toBeNull();
    expect(readLinkFragment('')).toBeNull();
  });
});
