import { createFileRoute } from '@tanstack/react-router';
import { ResetPasswordPage } from '@/features/auth/set-password-page';

/** Opened from the password reset email: /reset-password#uid=…&token=… */
export const Route = createFileRoute('/reset-password')({
  component: ResetPasswordPage,
});
