import { Link } from '@tanstack/react-router';
import { LoaderCircle } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useSignIn } from '@/api/generated/endpoints';
import type { SessionResponse } from '@/api/generated/model';
import { Button } from '@/components/ui/button';
import { AuthLayout } from './auth-layout';
import { Field, FormError } from './form';

/** Sign-in with email or mobile number (IAM-01). */
export function LoginPage({
  onSignedIn,
}: {
  onSignedIn: (session: SessionResponse) => void | Promise<void>;
}) {
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const signIn = useSignIn({ mutation: { onSuccess: onSignedIn } });

  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    signIn.mutate({ data: { identifier, password, rememberMe } });
  }

  return (
    <AuthLayout title="Sign in" description="Use your email address or mobile number.">
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <FormError error={signIn.error} />
        <Field
          id="identifier"
          label="Email or mobile number"
          autoComplete="username"
          required
          value={identifier}
          onChange={(e) => {
            setIdentifier(e.target.value);
          }}
        />
        <div className="flex flex-col gap-2">
          <Field
            id="password"
            label="Password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => {
              setPassword(e.target.value);
            }}
          />
          <Link
            to="/forgot-password"
            className="self-end text-xs text-muted-foreground underline-offset-4 hover:underline"
          >
            Forgot password?
          </Link>
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            className="size-4 accent-primary"
            checked={rememberMe}
            onChange={(e) => {
              setRememberMe(e.target.checked);
            }}
          />
          Keep me signed in for 14 days
          <span className="sr-only">(not on shared computers)</span>
        </label>
        <Button type="submit" disabled={signIn.isPending || !identifier || !password}>
          {signIn.isPending && <LoaderCircle className="animate-spin" aria-hidden />}
          Sign in
        </Button>
      </form>
    </AuthLayout>
  );
}
