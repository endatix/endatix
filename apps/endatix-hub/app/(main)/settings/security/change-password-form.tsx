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
import { FormErrorMessage } from '@/components/forms/form-error-message';

export function ChangePasswordForm() {
  const [state, formAction, isPending] = useActionState<
    ChangePasswordState,
    FormData
  >(changePasswordAction, {
    success: false,
  });

  return (
    <form action={formAction} className="space-y-8">
      <div className="grid gap-6">
        <div className="grid gap-2">
          <Label htmlFor="current">Current Password</Label>
          <Input
            id="currentPassword"
            name="currentPassword"
            type="password"
            placeholder="Enter your current password"
          />
          <p className="text-sm text-muted-foreground">
            Enter your current password to verify it&apos;s you
          </p>
          {state?.errors?.currentPassword && (
            <FormErrorMessage
              message={state.errors.currentPassword.toString()}
            />
          )}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="new">New Password</Label>
          <Input
            id="newPassword"
            name="newPassword"
            type="password"
            placeholder="Enter your new password"
          />
          <p className="text-sm text-muted-foreground">
            Password must be at least 8 characters long
          </p>
          {state?.errors?.newPassword && (
            <FormErrorMessage message={state.errors.newPassword.toString()} />
          )}
        </div>

        <div className="grid gap-2">
          <Label htmlFor="confirm">Confirm Password</Label>
          <Input
            id="confirmPassword"
            name="confirmPassword"
            type="password"
            placeholder="Confirm your new password"
          />
          <p className="text-sm text-muted-foreground">
            Re-enter your new password to confirm
          </p>
          {state?.errors?.confirmPassword && (
            <FormErrorMessage
              message={state.errors.confirmPassword.toString()}
            />
          )}
        </div>
      </div>

      <Button type="submit" disabled={isPending}>
        {isPending ? (
          <>
            <Spinner className="mr-2 h-4 w-4 animate-spin" />
            Changing...
          </>
        ) : (
          'Change password'
        )}
      </Button>

      {state?.errorMessage && <FormErrorMessage message={state.errorMessage} />}
    </form>
  );
}
