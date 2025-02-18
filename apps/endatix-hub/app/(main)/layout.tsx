import "./globals.css";
import type { Metadata } from "next";
import localFont from "next/font/local";
import { ThemeProvider } from "@/components/controls/theme/theme-provider";
import MainNav from "@/components/layout-ui/navigation/main-nav";
import { Toaster } from 'sonner';

const geistSans = localFont({
  src: "./fonts/GeistVF.woff",
  variable: "--font-geist-sans",
  weight: "100 900",
});
const geistMono = localFont({
  src: "./fonts/GeistMonoVF.woff",
  variable: "--font-geist-mono",
  weight: "100 900",
});

export const metadata: Metadata = {
  title: "Endatix Hub",
  description: "Your data on your terms",
};

interface RootLayoutProps {
  children: React.ReactNode;
  header: React.ReactNode;
}

export default function RootLayout({ children, header }: RootLayoutProps) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <link rel="icon" href="/assets/icons/icon.svg" type="image/svg+xml" />
      </head>
      <body className={`${geistSans.variable} ${geistMono.variable}`}>
        <ThemeProvider
          attribute="class"
          defaultTheme="light"
          enableSystem
          disableTransitionOnChange
        >
          <div className="flex min-h-screen w-full flex-col bg-muted/40">
            <aside className="fixed inset-y-0 left-0 z-10 hidden w-14 flex-col border-r bg-background sm:flex">
              <MainNav />
            </aside>
            <div className="flex flex-col sm:gap-4 sm:py-4 sm:pl-14">
              {header}
              <main className="grid flex-1 items-start gap-4 p-4 sm:px-6 sm:py-0 md:gap-8">
                {children}
              </main>
            </div>
          </div>
          <Toaster
            expand={false}
            duration={Infinity}
            visibleToasts={5}
          />
        </ThemeProvider>
      </body>
    </html>
  );
}
