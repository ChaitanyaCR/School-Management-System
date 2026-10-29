import { createFileRoute } from '@tanstack/react-router';
import { SystemStatusCard } from '@/features/system/system-status-card';

export const Route = createFileRoute('/_app/')({
  component: Dashboard,
});

function Dashboard() {
  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground">Modules will appear here as they are delivered.</p>
      </div>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <SystemStatusCard />
      </div>
    </div>
  );
}
