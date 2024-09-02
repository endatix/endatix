"use client";

import { signIn } from "@/actions/sign-in";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import Link from "next/link";
import { useActionState } from "react";

const SignInForm = () => {
  const [state, formAction] = useActionState(signIn, null);

  return (
    <form action={formAction}>
      <div className="grid gap-2 text-center">
        <h1 className="text-3xl font-bold">Login</h1>
        <p className="text-balance text-muted-foreground">
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
        {state?.errors?.email && <p>{state.errors.email}</p>}
        <div className="grid gap-2">
          <div className="flex items-center">
            <Label htmlFor="password">Password</Label>
            <Link
              href="/forgot-password"
              className="ml-auto inline-block text-sm underline"
            >
              Forgot your password?
            </Link>
          </div>
          <Input id="password" type="password" name="password" required />
        </div>
        {state?.errors?.password && (
          <p>Password must {state.errors.password}</p>
        )}
        <Button type="submit" className="w-full">
          Login
        </Button>
      </div>
      {!state?.success && state?.errors?.primary && (
        <p>{state?.errors?.primary}</p>
      )}
    </form>
  );
};

export default SignInForm;
