import { Link } from '@tanstack/react-router';
import { LoaderCircle, MailCheck } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useForgotPassword } from '@/api/generated/endpoints';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { AuthLayout } from './auth-layout';
import { Field, FormError } from './form';

/** Requests a reset link (IAM-03). The answer is the same whether or not the account exists. */
export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const forgot = useForgotPassword();

  function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    forgot.mutate({ data: { email } });
  }

  return (
    <AuthLayout
      title="Reset your password"
      description="We will email you a link to choose a new password."
    >
      {forgot.isSuccess ? (
        <div className="flex flex-col gap-4">
          <Alert>
            <MailCheck aria-hidden />
            <AlertTitle>Check your email</AlertTitle>
            <AlertDescription>
              If an account uses {email}, a reset link is on its way. It works for one hour.
            </AlertDescription>
          </Alert>
          <BackToSignIn />
        </div>
      ) : (
        <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
          <FormError error={forgot.error} />
          <Field
            id="email"
            label="Email address"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => {
              setEmail(e.target.value);
            }}
          />
          <Button type="submit" disabled={forgot.isPending || !email}>
            {forgot.isPending && <LoaderCircle className="animate-spin" aria-hidden />}
            Send reset link
          </Button>
          <BackToSignIn />
        </form>
      )}
    </AuthLayout>
  );
}

export function BackToSignIn() {
  return (
    <Link
      to="/login"
      className="text-center text-sm text-muted-foreground underline-offset-4 hover:underline"
    >
      Back to sign in
    </Link>
  );
}
