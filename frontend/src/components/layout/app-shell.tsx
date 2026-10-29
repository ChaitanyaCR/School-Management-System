import { Link } from '@tanstack/react-router';
import { GraduationCap } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import { useGetSystemInfo } from '@/api/generated/endpoints';

const FALLBACK_PRODUCT_NAME = 'School Management System';

/** Page frame: top bar with the product name (from the API, so branding is configuration) and content area. */
export function AppShell({ children }: { children: ReactNode }) {
  const { data: info } = useGetSystemInfo();
  const productName = info?.productName ?? FALLBACK_PRODUCT_NAME;

  useEffect(() => {
    document.title = productName;
  }, [productName]);

  return (
    <div className="flex min-h-svh flex-col">
      <header className="sticky top-0 z-10 border-b bg-card/95 backdrop-blur">
        <div className="mx-auto flex h-14 w-full max-w-7xl items-center gap-3 px-4">
          <Link to="/" className="flex items-center gap-2 font-semibold">
            <GraduationCap className="size-6 text-primary" aria-hidden />
            <span>{productName}</span>
          </Link>
        </div>
      </header>
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6">{children}</main>
    </div>
  );
}
