import { useQueryClient } from '@tanstack/react-query';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { setSession } from '@/features/auth/session';
import { AcceptInvitePage } from '@/features/auth/set-password-page';

/** Opened from the invitation email: /accept-invite#uid=…&token=… */
export const Route = createFileRoute('/accept-invite')({
  component: AcceptInviteRoute,
});

function AcceptInviteRoute() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return (
    <AcceptInvitePage
      onAccepted={async (session) => {
        setSession(queryClient, session);
        await navigate({ to: '/' });
      }}
    />
  );
}
