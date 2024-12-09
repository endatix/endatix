import Image from "next/image";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import GitHubIcon from "@/public/assets/icons/github.svg";
import { BookText, Globe } from "lucide-react";
import { Metadata } from "next";

export const metadata: Metadata = {
  title: 'Home | Endatix Hub',
  description: "Endatix Hub's homepage. The starting point of getting things done."
}

const Home = () => {
  return (
    <div className="grid h-full grid-rows-[20px_1fr_20px] items-center justify-items-center p-8 gap-16 sm:p-20 font-[family-name:var(--font-geist-sans)]">
      <div className="absolute top-0 right-0 left-0 bottom-0 bg-[url('/lines-and-stuff.svg')] brightness-[0.6] opacity-[0.4] dark:grayscale"></div>
      <div className="z-10 items-start flex flex-col gap-4 row-start-2 items-center sm:items-start">
        <Card className="sm:col-span-4 auto-rows-max bg-background">
          <CardHeader className="pb-3">
            <CardTitle>
              <Image
                src="/assets/icons/endatix.svg"
                alt="Endatix logo"
                width={180}
                height={38}
                priority
              />
            </CardTitle>
            <CardDescription className="max-w-lg text-balance leading-relaxed">
              Endatix Hub is the new exciting way to manage your data collection
              and processing workflows.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <p>Coming soon!</p>
          </CardContent>
          <CardFooter>
            <Button className="mr-8">
              <a href="https://endatix.com?utm_source=endatix-hub&utm_medium=product">
                Learn about Endatix
              </a>
            </Button>
            <Button variant="secondary">
              <a href="https://docs.endatix.com/docs/category/getting-started?utm_source=endatix-hub&utm_medium=product">
                Read our Docs
              </a>
            </Button>
          </CardFooter>
        </Card>
      </div>
      <footer className="z-10 row-start-3 flex gap-6 flex-wrap items-center justify-center">
        <a
          className="flex items-center gap-2 hover:underline hover:underline-offset-4"
          href="https://docs.endatix.com?utm_source=endatix-hub&utm_medium=product"
          target="_blank"
          rel="noopener noreferrer"
        >
          <BookText width={16} height={16} />
          Learn
        </a>
        <a
          className="flex items-center gap-2 hover:underline hover:underline-offset-4"
          href="https://github.com/endatix/endatix?tab=readme-ov-file#endatix-platform"
          target="_blank"
          rel="noopener noreferrer"
        >
          <Image
            aria-hidden
            className="dark:invert"
            src={GitHubIcon}
            alt="GitHub icon"
            width={16}
            height={16}
          />
          Follow us on GitHub
        </a>
        <a
          className="flex items-center gap-2 hover:underline hover:underline-offset-4"
          href="https://endatix.com?utm_source=endatix-hub"
          target="_blank"
          rel="noopener noreferrer"
        >
          <Globe width={16} height={16} />
          Go to endatix.com →
        </a>
      </footer>
    </div>
  );
}

export default Home;
