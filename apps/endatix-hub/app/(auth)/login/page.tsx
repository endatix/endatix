import Image from "next/image";
import Link from "next/link";
import LoginForm from "./ui/login-form";
import type { Metadata } from 'next'
import { showComingSoonMessage } from "@/components/layout-ui/utils/coming-soon-message";
import NewAccountLink from "./ui/new-account-link";

export const metadata: Metadata = {
  title: 'Login',
  description: "Endatix sign in page. The first column has the login form with email and password. There's a Forgot your password link and a link to sign up if you do not have an account. The second column has a cover image."
}

export default function LoginPage() {

  return (
    <div className="w-full lg:grid lg:min-h-[600px] lg:grid-cols-2 xl:min-h-[800px]">
      <div className="flex items-center justify-center py-12">
        <div className="mx-auto grid w-[350px] gap-6">
          <LoginForm />
          <NewAccountLink />
        </div>
      </div>
      <div className="hidden bg-muted lg:block">
        <Image
          src="/lines-and-stuff.svg"
          alt="Lines and dots pattern"
          width="1920"
          height="1080"
          className="h-full w-full object-cover dark:brightness-[0.6] dark:grayscale"
        />
      </div>
    </div>
  );
}