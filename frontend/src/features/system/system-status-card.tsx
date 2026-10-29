import { CircleAlert, CircleCheck, LoaderCircle } from 'lucide-react';
import { useGetSystemInfo } from '@/api/generated/endpoints';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

/** Shows whether the UI can reach the API, plus version and environment. */
export function SystemStatusCard() {
  const { data, isPending, isError } = useGetSystemInfo();

  return (
    <Card>
      <CardHeader>
        <CardTitle>System status</CardTitle>
        <CardDescription>Connection between this app and the API server.</CardDescription>
      </CardHeader>
      <CardContent>
        {isPending ? (
          <p className="flex items-center gap-2 text-muted-foreground">
            <LoaderCircle className="size-4 animate-spin" aria-hidden />
            Checking connection…
          </p>
        ) : isError ? (
          <p className="flex items-center gap-2 text-destructive" role="alert">
            <CircleAlert className="size-4" aria-hidden />
            Cannot reach the API server.
          </p>
        ) : (
          <div className="flex flex-col gap-3">
            <p className="flex items-center gap-2 text-success">
              <CircleCheck className="size-4" aria-hidden />
              Connected
            </p>
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
              <dt className="text-muted-foreground">Version</dt>
              <dd>{data.version}</dd>
              <dt className="text-muted-foreground">Environment</dt>
              <dd>
                <Badge variant="secondary">{data.environment}</Badge>
              </dd>
            </dl>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
