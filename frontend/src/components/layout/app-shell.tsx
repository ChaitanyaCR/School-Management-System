import { Link } from '@tanstack/react-router';
import { GraduationCap } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import { useGetCurrentOrganisation, useGetSystemInfo } from '@/api/generated/endpoints';

const FALLBACK_PRODUCT_NAME = 'School Management System';

/**
 * Page frame: top bar with the organisation served at this subdomain (or the product name on the bare
 * domain), optional actions on the right (e.g. the user menu), and the content area. Branding comes from
 * the API, so it is configuration, not code.
 */
export function AppShell({ children, actions }: { children: ReactNode; actions?: ReactNode }) {
  const { data: info } = useGetSystemInfo();
  // 404 on the bare domain is expected (no organisation), so don't retry.
  const { data: organisation } = useGetCurrentOrganisation({ query: { retry: false } });

  const productName = info?.productName ?? FALLBACK_PRODUCT_NAME;
  const title = organisation?.name ?? productName;

  useEffect(() => {
    document.title = organisation ? `${organisation.name} · ${productName}` : productName;
  }, [organisation, productName]);

  return (
    <div className="flex min-h-svh flex-col">
      <header className="sticky top-0 z-10 border-b bg-card/95 backdrop-blur">
        <div className="mx-auto flex h-14 w-full max-w-7xl items-center gap-3 px-4">
          <Link to="/" className="flex min-w-0 items-center gap-2 font-semibold">
            <GraduationCap className="size-6 shrink-0 text-primary" aria-hidden />
            <span className="truncate">{title}</span>
          </Link>
          <div className="ml-auto flex items-center gap-4">
            {organisation && (
              <span className="hidden text-sm text-muted-foreground md:inline">{productName}</span>
            )}
            {actions}
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6">{children}</main>
    </div>
  );
}
