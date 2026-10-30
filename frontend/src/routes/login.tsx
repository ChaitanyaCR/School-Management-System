import { useQueryClient } from '@tanstack/react-query';
import { createFileRoute, redirect, useRouter } from '@tanstack/react-router';
import { LoginPage } from '@/features/auth/login-page';
import { loadSession, safeRedirect, setSession } from '@/features/auth/session';

interface LoginSearch {
  /** The page to return to after signing in. */
  redirect?: string;
}

export const Route = createFileRoute('/login')({
  validateSearch: (search: Record<string, unknown>): LoginSearch => ({
    redirect: typeof search.redirect === 'string' ? search.redirect : undefined,
  }),
  beforeLoad: async ({ context, search }) => {
    const session = await loadSession(context.queryClient);
    if (session.user) {
      throw redirect({ href: safeRedirect(search.redirect) });
    }
  },
  component: LoginRoute,
});

function LoginRoute() {
  const queryClient = useQueryClient();
  const router = useRouter();

  return (
    <LoginPage
      onSignedIn={async (session) => {
        setSession(queryClient, session);
        // Re-running this route's beforeLoad sends the now signed-in user on.
        await router.invalidate();
      }}
    />
  );
}
