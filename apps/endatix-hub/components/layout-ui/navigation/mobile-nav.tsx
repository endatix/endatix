import Image from 'next/image';
import NavLink from './nav-link';
import Link from 'next/link';
import EndatixLogoSvg from '@/public/assets/icons/icon.svg';
import { SitemapService } from '@/services/sitemap-service';
import { Sheet, SheetContent, SheetTrigger } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { PanelLeft } from 'lucide-react';

const MobileNav = () => {
  const logo = SitemapService.getLogo();
  const sitemapList = SitemapService.getTopLevelSitemap();
  return (
    <Sheet>
      <SheetTrigger asChild>
        <Button size="icon" variant="outline" className="sm:hidden">
          <PanelLeft className="h-5 w-5" />
          <span className="sr-only">Toggle Menu</span>
        </Button>
      </SheetTrigger>
      <SheetContent side="left" className="sm:max-w-xs">
        <nav className="grid gap-6 text-lg font-medium">
          <Link
            href={logo.path}
            className="group flex h-10 w-10 shrink-0 items-center justify-center gap-2 rounded-sm text-lg font-semibold text-primary-foreground md:text-base"
          >
            <Image
              aria-hidden
              className="h-8 w-8 transition-all group-hover:scale-110 rounded-sm"
              src={EndatixLogoSvg}
              alt="logo"
            />
            <span className="sr-only">{logo.text}</span>
          </Link>
          {sitemapList.map((navItem) => (
            <NavLink
              key={navItem.text}
              text={navItem.text}
              path={navItem.path}
              className="flex items-center gap-4 px-2.5 text-muted-foreground hover:text-foreground"
            >
              <navItem.IconType className="h-5 w-5" />
              {navItem.text}
            </NavLink>
          ))}
        </nav>
      </SheetContent>
    </Sheet>
  );
};

export default MobileNav;
