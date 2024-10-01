"use client";

import { showComingSoonMessage } from "@/components/layout-ui/utils/coming-soon-message";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import Link from "next/link";
import { useActionState } from "react";
import { loginAction } from "../login.action";

const LoginForm = () => {
  const [state, formAction] = useActionState(loginAction, null);

  return (
    <form action={formAction}>
      <div className="grid gap-2 text-center">
        <h1 className="text-3xl font-bold">Login</h1>
        <p className="mb-6 text-balance text-muted-foreground">
          Enter your email below to login to your account
        </p>
      </div>
      <div className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="email">Email</Label>
          <Input
            id="email"
            type="email"
            name="email"
            placeholder="your.email@example.com"
            required
          />
        </div>
        {state?.errors?.email && <ErrorMessage message={state.errors.email.toString()} />}
        <div className="grid gap-2">
          <div className="flex items-center">
            <Label htmlFor="password">Password</Label>
            <Link
              href="#"
              onClick={(e) => showComingSoonMessage(e)}
              className="ml-auto inline-block text-sm underline"
            >
              Forgot your password?
            </Link>
          </div>
          <Input id="password" type="password" name="password" required />
        </div>
        {state?.errors?.password && (
          <ErrorMessage message={`Password must ${state.errors.password}`} />
        )}
        <Button type="submit" className="w-full">
          Login
        </Button>
      </div>
      {state?.errorMessage && <ErrorMessage message={state.errorMessage} />}
    </form>
  );
};

export interface ErrorMessageProps {
  message: string
}

const ErrorMessage = ({ message }: ErrorMessageProps) => {
  return (
    <p className="mt-2 text-sm font-medium text-destructive">{message}</p>
  )
}

export default LoginForm;