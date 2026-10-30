import { Link, useLocation } from '@tanstack/react-router';
import { CircleCheck, LoaderCircle } from 'lucide-react';
import { useEffect, useState, type SubmitEvent } from 'react';
import { useAcceptInvitation, useResetPassword } from '@/api/generated/endpoints';
import type { SessionResponse } from '@/api/generated/model';
import { ApiError } from '@/api/http';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { AuthLayout } from './auth-layout';
import { BackToSignIn } from './forgot-password-page';
import { Field, FormError } from './form';
import { PASSWORD_HINT, fieldErrors } from './form-errors';
import { readLinkFragment } from './session';

/**
 * Reads the user id and token from the emailed link once, then removes them from the address bar
 * so they do not linger in the browser history.
 */
function useEmailedLink() {
  const hash = useLocation({ select: (location) => location.hash });
  const [link] = useState(() => readLinkFragment(hash));

  useEffect(() => {
    if (link && window.location.hash) {
      window.history.replaceState(window.history.state, '', window.location.pathname);
    }
  }, [link]);

  return link;
}

function InvalidLink({ action }: { action: string }) {
  return (
    <div className="flex flex-col gap-4">
      <Alert variant="destructive">
        <AlertTitle>This link is invalid or has expired</AlertTitle>
        <AlertDescription>Links work once and expire. {action}</AlertDescription>
      </Alert>
      <BackToSignIn />
    </div>
  );
}

interface PasswordFormProps {
  submitLabel: string;
  isPending: boolean;
  error: unknown;
  onSubmit: (password: string) => void;
}

function PasswordForm({ submitLabel, isPending, error, onSubmit }: PasswordFormProps) {
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const mismatch = confirmation.length > 0 && confirmation !== password;
  const passwordErrors = fieldErrors(error, 'password');

  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!mismatch) {
      onSubmit(password);
    }
  }

  return (
    <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
      {passwordErrors.length === 0 && <FormError error={error} />}
      <Field
        id="password"
        label="New password"
        type="password"
        autoComplete="new-password"
        required
        hint={PASSWORD_HINT}
        errors={passwordErrors}
        value={password}
        onChange={(e) => {
          setPassword(e.target.value);
        }}
      />
      <Field
        id="confirmation"
        label="Confirm new password"
        type="password"
        autoComplete="new-password"
        required
        errors={mismatch ? ['The passwords do not match.'] : []}
        value={confirmation}
        onChange={(e) => {
          setConfirmation(e.target.value);
        }}
      />
      <Button type="submit" disabled={isPending || !password || mismatch || !confirmation}>
        {isPending && <LoaderCircle className="animate-spin" aria-hidden />}
        {submitLabel}
      </Button>
    </form>
  );
}

const isInvalidToken = (error: unknown) =>
  error instanceof ApiError && error.code === 'identity.invalid_token';

/** Sets the first password from an invitation link (IAM-02); the user is then signed in. */
export function AcceptInvitePage({
  onAccepted,
}: {
  onAccepted: (session: SessionResponse) => void | Promise<void>;
}) {
  const link = useEmailedLink();
  const accept = useAcceptInvitation({ mutation: { onSuccess: onAccepted } });
  const askAgain = 'Ask your school administrator to send a new invitation.';

  return (
    <AuthLayout title="Welcome" description="Choose a password to activate your account.">
      {!link || isInvalidToken(accept.error) ? (
        <InvalidLink action={askAgain} />
      ) : (
        <PasswordForm
          submitLabel="Activate account"
          isPending={accept.isPending}
          error={accept.error}
          onSubmit={(password) => {
            accept.mutate({ data: { userId: link.userId, token: link.token, password } });
          }}
        />
      )}
    </AuthLayout>
  );
}

/** Sets a new password from a reset link (IAM-03). */
export function ResetPasswordPage() {
  const link = useEmailedLink();
  const reset = useResetPassword();
  const askAgain = 'Request a new reset link.';

  return (
    <AuthLayout title="Choose a new password">
      {reset.isSuccess ? (
        <div className="flex flex-col gap-4">
          <Alert>
            <CircleCheck aria-hidden />
            <AlertTitle>Password changed</AlertTitle>
            <AlertDescription>
              Sign in with your new password. Other devices are signed out within a few minutes.
            </AlertDescription>
          </Alert>
          <Button asChild>
            <Link to="/login">Sign in</Link>
          </Button>
        </div>
      ) : !link || isInvalidToken(reset.error) ? (
        <InvalidLink action={askAgain} />
      ) : (
        <PasswordForm
          submitLabel="Change password"
          isPending={reset.isPending}
          error={reset.error}
          onSubmit={(password) => {
            reset.mutate({ data: { userId: link.userId, token: link.token, password } });
          }}
        />
      )}
    </AuthLayout>
  );
}
