import NotificationsBell from "@/components/controls/notifications/notifications-bell";
import MyAccountDropdown from "@/components/layout-ui/my-account/my-account-dropdown";
import BreadcrumbNav from "@/components/layout-ui/navigation/breadcrumb-nav";
import MainSearchBar from "@/components/layout-ui/navigation/main-search-bar";
import MobileNav from "@/components/layout-ui/navigation/mobile-nav";
import { SitemapService } from "@/services/sitemap-service";

interface MainHeaderProps {
  showHeader?: boolean;
}

export default function MainHeader({ showHeader = true }: MainHeaderProps) {
  const sitemap = SitemapService.getSitemap();

  if (!showHeader) return null;

  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-4 border-b bg-background px-4 sm:static sm:h-auto sm:border-0 sm:bg-transparent sm:px-6">
      <MobileNav />
      <BreadcrumbNav homeText="Home" sitemap={sitemap}></BreadcrumbNav>
      <MainSearchBar />
      <NotificationsBell badgeStyle="badge" renderSampleData={false} />
      <MyAccountDropdown />
    </header>
  );
}
