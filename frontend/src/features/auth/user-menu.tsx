import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { LogOut } from 'lucide-react';
import { useSignOut } from '@/api/generated/endpoints';
import { Button } from '@/components/ui/button';
import { useCurrentUser } from './session';

/** Signed-in user's name and sign-out. Signing out clears every cached response (shared computers). */
export function UserMenu() {
  const user = useCurrentUser();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const signOut = useSignOut({
    mutation: {
      // Even if the server call fails (e.g. the session already expired), leave nothing behind.
      onSettled: async () => {
        queryClient.clear();
        await navigate({ to: '/login' });
      },
    },
  });

  if (!user) {
    return null;
  }

  return (
    <div className="flex items-center gap-3">
      <span className="hidden text-sm sm:inline" title={user.email}>
        {user.fullName}
      </span>
      <Button
        variant="ghost"
        size="sm"
        disabled={signOut.isPending}
        onClick={() => {
          signOut.mutate();
        }}
      >
        <LogOut aria-hidden />
        Sign out
      </Button>
    </div>
  );
}
