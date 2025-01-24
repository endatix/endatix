import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
  TooltipProvider,
} from '@/components/ui/tooltip';
import Image from 'next/image';
import NavLink from './nav-link';
import { sitemap } from '@/lib/constants';
import { SitemapService } from '@/services/sitemap-service';
import EndatixLogoSvg from '@/public/assets/icons/icon.svg';

const MainNav = () => {
  const logo = SitemapService.getLogo();
  const sitemapList = SitemapService.getTopLevelSitemap(true);
  const settingsNavItem = sitemap.Settings;
  return (
    <TooltipProvider>
      <nav className="flex flex-col items-center gap-4 px-2 sm:py-5 space-y-1">
        <NavLink
          path={logo.path}
          text={logo.text}
          setIsActive={false}
          className="group flex h-10 w-10 shrink-0 items-center justify-center rounded-sm gap-2 bg-primary text-lg font-semibold text-primary-foreground md:h-9 md:w-9 md:text-base"
        >
          <Image
            aria-hidden
            className="h-9 w-9 transition-all group-hover:scale-110 rounded-sm"
            src={EndatixLogoSvg}
            alt="logo"
          />
        </NavLink>
        {sitemapList.map((navItem) => (
          <Tooltip key={navItem.text}>
            <TooltipTrigger asChild={false}>
              <NavLink path={navItem.path} text={navItem.text}>
                <navItem.IconType className="h-6 w-6" />
              </NavLink>
            </TooltipTrigger>
            <TooltipContent side="right">{navItem.text}</TooltipContent>
          </Tooltip>
        ))}
      </nav>
      <nav className="mt-auto flex flex-col items-center gap-4 px-2 sm:py-5">
        <NavLink path={settingsNavItem.path} text={settingsNavItem.text}>
          <settingsNavItem.IconType className="h-6 w-6" />
        </NavLink>
      </nav>
    </TooltipProvider>
  );
};

export default MainNav;
