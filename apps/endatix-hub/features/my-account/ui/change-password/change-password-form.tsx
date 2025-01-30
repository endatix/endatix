'use client';

import { Spinner } from '@/components/loaders/spinner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useActionState } from 'react';
import {
  changePasswordAction,
  ChangePasswordState,
} from '@/features/my-account/application/actions';
import { ErrorMessage } from '@/components/forms/error-message';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { CheckIcon } from 'lucide-react';

function ChangePasswordForm() {
  const [state, formAction, isPending] = useActionState<
    ChangePasswordState,
    FormData
  >(changePasswordAction, {
    success: false,
  });

  if (state.success) {
    return (
      <Alert variant="default">
        <AlertTitle className="flex items-center gap-2">
          <CheckIcon className="h-4 w-4" />
          Success
        </AlertTitle>
        <AlertDescription>
          Your password has been changed successfully.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <form action={formAction} className="space-y-8 mt-8">
      <div className="grid gap-6">
        <div className="grid gap-2">
          <Label htmlFor="currentPassword">Current Password</Label>
          <Input
            id="currentPassword"
            name="currentPassword"
            type="password"
            autoComplete="current-password"
            placeholder="Enter your current password"
          />
          <p className="text-sm text-muted-foreground">
            Enter your current password to verify it&apos;s you
          </p>
          {state?.errors?.currentPassword && (
            <ErrorMessage
              message={state.errors.currentPassword.toString()}
            />
          )}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="newPassword">New Password</Label>
          <Input
            id="newPassword"
            name="newPassword"
            type="password"
            autoComplete="new-password"
            placeholder="Enter your new password"
          />
          <p className="text-sm text-muted-foreground">
            Password must be at least 8 characters long
          </p>
          {state?.errors?.newPassword && (
            <ErrorMessage message={state.errors.newPassword.toString()} />
          )}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="confirmPassword">Confirm Password</Label>
          <Input
            id="confirmPassword"
            name="confirmPassword"
            type="password"
            autoComplete="new-password"
            placeholder="Confirm your new password"
          />
          <p className="text-sm text-muted-foreground">
            Re-enter your new password to confirm
          </p>
          {state?.errors?.confirmPassword && (
            <ErrorMessage
              message={state.errors.confirmPassword.toString()}
            />
          )}
        </div>
      </div>

      <Button type="submit" disabled={isPending}>
        {isPending ? (
          <>
            <Spinner className="mr-2 h-4 w-4" />
            Changing...
          </>
        ) : (
          'Change password'
        )}
      </Button>

      {state?.errorMessage && <ErrorMessage message={state.errorMessage} />}
    </form>
  );
}

export { ChangePasswordForm };
