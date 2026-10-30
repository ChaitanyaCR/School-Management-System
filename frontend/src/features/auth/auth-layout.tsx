import { GraduationCap } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import { useGetCurrentOrganisation, useGetSystemInfo } from '@/api/generated/endpoints';
import { ApiError } from '@/api/http';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

const FALLBACK_PRODUCT_NAME = 'School Management System';

/**
 * Frame for the pages used before signing in. Each school signs in at its own address; on any
 * other address (e.g. the bare domain) the forms are replaced by a hint.
 */
export function AuthLayout({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  const { data: info } = useGetSystemInfo();
  const { data: organisation, error } = useGetCurrentOrganisation({ query: { retry: false } });
  const productName = info?.productName ?? FALLBACK_PRODUCT_NAME;
  const noSchool = error instanceof ApiError && error.status === 404;

  useEffect(() => {
    document.title = `${title} · ${organisation?.name ?? productName}`;
  }, [title, organisation, productName]);

  return (
    <div className="flex min-h-svh flex-col items-center justify-center gap-6 bg-muted/40 px-4 py-10">
      <div className="flex items-center gap-2 text-lg font-semibold">
        <GraduationCap className="size-7 text-primary" aria-hidden />
        <span>{organisation?.name ?? productName}</span>
      </div>
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1 className="text-xl">{title}</h1>
          </CardTitle>
          {description && <CardDescription>{description}</CardDescription>}
        </CardHeader>
        <CardContent>
          {noSchool ? (
            <Alert>
              <AlertTitle>Use your school&apos;s address</AlertTitle>
              <AlertDescription>
                Each school has its own address, such as yourschool.{window.location.host}. Open the
                link your school gave you.
              </AlertDescription>
            </Alert>
          ) : (
            children
          )}
        </CardContent>
      </Card>
      {organisation && <p className="text-xs text-muted-foreground">{productName}</p>}
    </div>
  );
}
