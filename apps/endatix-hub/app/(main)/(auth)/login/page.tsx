import Image from "next/image";
import LoginForm from "./ui/login-form";
import type { Metadata } from 'next';
import NewAccountLink from "./ui/new-account-link";
import { getSession } from "@/lib/auth-service";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Rocket } from "lucide-react";
import { Button } from "@/components/ui/button";
import Link from "next/link";

export const metadata: Metadata = {
  title: 'Login | Endatix Hub',
  description: "Endatix sign in page. The first column has the login form with email and password. There's a Forgot your password link and a link to sign up if you do not have an account. The second column has a cover image."
}

const LoginPage = async () => {
  const user = await getSession();
  return (
    <div className="w-full lg:grid lg:min-h-[600px] lg:grid-cols-2 xl:min-h-[800px]">
      <div className="flex items-center justify-center py-12">
        <div className="mx-auto grid w-[400px] gap-6">
          {user.isLoggedIn ?
            <LoggedInSuccessMessage
              username={user.username}
              isLoggedIn={user.isLoggedIn} /> :
            <LoginFormWrapper />
          }
        </div>
      </div>
      <div className="hidden bg-muted lg:block">
        <Image
          src="/assets/lines-and-stuff.svg"
          alt="Lines and dots pattern"
          width="1920"
          height="1080"
          className="h-full w-full object-cover dark:brightness-[0.6] dark:grayscale"
        />
      </div>
    </div>
  );
}


interface LoggedInMessageProps {
  username: string,
  isLoggedIn: boolean;
}

const LoggedInSuccessMessage = ({ username, isLoggedIn }: LoggedInMessageProps) => {
  if (isLoggedIn) return (
    <Card className="bg-background">
      <CardHeader className="pb-3">
        <CardTitle>Welcome!
        </CardTitle>
        <CardDescription className="max-w-lg text-balance leading-relaxed">
          You are now logged in as {username}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <p>Click on the button below to continue</p>
      </CardContent>
      <CardFooter>
        <Link href="/">
          <Button className="mr-8">
            <Rocket className="mr-2 h-4 w-4" />Continue
          </Button>
        </Link>
      </CardFooter>
    </Card>
  );
}

const LoginFormWrapper = () => (
  <>
    <LoginForm />
    <NewAccountLink />
  </>
)

export default LoginPage;