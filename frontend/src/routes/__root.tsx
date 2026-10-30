import type { QueryClient } from '@tanstack/react-query';
import { createRootRouteWithContext, Link, Outlet } from '@tanstack/react-router';
import { Button } from '@/components/ui/button';

export interface RouterContext {
  queryClient: QueryClient;
}

/**
 * Pages that need a signed-in user live under the `_app` layout; the sign-in pages
 * (login, invitation, password reset) are top-level routes with their own frame.
 */
export const Route = createRootRouteWithContext<RouterContext>()({
  component: Outlet,
  notFoundComponent: NotFound,
});

function NotFound() {
  return (
    <div className="mx-auto flex max-w-7xl flex-col items-start gap-4 px-4 py-10">
      <h1 className="text-2xl font-semibold">Page not found</h1>
      <p className="text-muted-foreground">The page you are looking for does not exist.</p>
      <Button asChild variant="outline">
        <Link to="/">Back to dashboard</Link>
      </Button>
    </div>
  );
}
