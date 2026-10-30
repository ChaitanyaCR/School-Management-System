import { createFileRoute, Outlet, redirect } from '@tanstack/react-router';
import { AppShell } from '@/components/layout/app-shell';
import { UserMenu } from '@/features/auth/user-menu';
import { loadSession } from '@/features/auth/session';

/** Layout for every page that needs a signed-in user; others are sent to sign in first. */
export const Route = createFileRoute('/_app')({
  beforeLoad: async ({ context, location }) => {
    const session = await loadSession(context.queryClient);
    if (!session.user) {
      throw redirect({ to: '/login', search: { redirect: location.href } });
    }
  },
  component: AppLayout,
});

function AppLayout() {
  return (
    <AppShell actions={<UserMenu />}>
      <Outlet />
    </AppShell>
  );
}
