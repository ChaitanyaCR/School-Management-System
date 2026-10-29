import { CircleAlert } from 'lucide-react';
import type { ComponentProps } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { errorMessage } from './form-errors';

export function FormError({ error }: { error: unknown }) {
  if (!error) {
    return null;
  }
  return (
    <Alert variant="destructive">
      <CircleAlert aria-hidden />
      <AlertDescription>{errorMessage(error)}</AlertDescription>
    </Alert>
  );
}

interface FieldProps extends ComponentProps<typeof Input> {
  id: string;
  label: string;
  hint?: string;
  errors?: string[];
}

/** Label, input, hint and per-field errors, wired up for screen readers. */
export function Field({ id, label, hint, errors = [], ...inputProps }: FieldProps) {
  const describedBy = [hint && `${id}-hint`, errors.length > 0 && `${id}-errors`]
    .filter(Boolean)
    .join(' ');
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        aria-invalid={errors.length > 0 || undefined}
        aria-describedby={describedBy || undefined}
        {...inputProps}
      />
      {hint && (
        <p id={`${id}-hint`} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
      {errors.length > 0 && (
        <ul id={`${id}-errors`} className="flex flex-col gap-1 text-xs text-destructive">
          {errors.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
