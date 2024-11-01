import type { Metadata } from "next"
import "./globals.css"
import localFont from "next/font/local"
import { ThemeProvider } from "@/components/controls/theme/theme-provider"
import { PanelLeft } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Sheet, SheetContent, SheetTrigger } from "@/components/ui/sheet"
import MainNav from "@/components/layout-ui/navigation/main-nav"
import MobileNav from "@/components/layout-ui/navigation/mobile-nav"
import BreadcrumbNav from "@/components/layout-ui/navigation/breadcrumb-nav"
import { SitemapService } from "@/services/sitemap-service"
import { Toaster } from "sonner"
import MainSearchBar from "@/components/layout-ui/navigation/main-search-bar"
import NotificationsBell from "@/components/controls/notifications/notifications-bell"
import MyAccountDropdown from "@/components/layout-ui/my-account/my-account-dropdown"

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
  children: React.ReactNode
}

export default function RootLayout({ children }: RootLayoutProps) {
  const sitemap = SitemapService.getSitemap();
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <link rel="icon" href="/icons/icon.svg" type="image/svg+xml" />
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
              <header className="sticky top-0 z-30 flex h-14 items-center gap-4 border-b bg-background px-4 sm:static sm:h-auto sm:border-0 sm:bg-transparent sm:px-6">
                <Sheet>
                  <SheetTrigger asChild>
                    <Button size="icon" variant="outline" className="sm:hidden">
                      <PanelLeft className="h-5 w-5" />
                      <span className="sr-only">Toggle Menu</span>
                    </Button>
                  </SheetTrigger>
                  <SheetContent side="left" className="sm:max-w-xs">
                    <MobileNav />
                  </SheetContent>
                </Sheet>
                <BreadcrumbNav
                  homeText="Home"
                  sitemap={sitemap}
                ></BreadcrumbNav>
                <MainSearchBar />
                <NotificationsBell
                  badgeStyle="badge"
                  renderSampleData={false}
                />
                <MyAccountDropdown />
              </header>
              <main className="grid flex-1 items-start gap-4 p-4 sm:px-6 sm:py-0 md:gap-8">
                {children}
              </main>
            </div>
          </div>
          <Toaster />
        </ThemeProvider>
      </body>
    </html>
  );
}
