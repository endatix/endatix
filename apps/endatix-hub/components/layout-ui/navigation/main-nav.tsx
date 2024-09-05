import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
  TooltipProvider,
} from "@/components/ui/tooltip";
import NavLink from "./nav-link";
import { sitemap } from "@/lib/constants";
import { SitemapService } from "@/services/sitemap-service";

const MainNav = () => {
  const logo = SitemapService.getLogo();
  const sitemapList = SitemapService.getTopLevelSitemap(true);
  const settingsNavItem = sitemap.Settings;
  return (
    <TooltipProvider>
      <nav className="flex flex-col items-center gap-4 px-2 sm:py-5">
        <NavLink
          path={logo.path}
          text={logo.text}
          setIsActive={false}
          className="group flex h-9 w-9 shrink-0 items-center justify-center gap-2 rounded-full bg-primary text-lg font-semibold text-primary-foreground md:h-8 md:w-8 md:text-base"
        >
          <logo.IconType className="h-4 w-4 transition-all group-hover:scale-110" />
        </NavLink>
        {sitemapList.map((navItem) => (
          <Tooltip key={navItem.path}>
            <TooltipTrigger asChild={false}>
              <NavLink path={navItem.path} text={navItem.text}>
                <navItem.IconType className="h-5 w-5" />
              </NavLink>
            </TooltipTrigger>
            <TooltipContent side="right">{navItem.text}</TooltipContent>
          </Tooltip>
        ))}
      </nav>
      <nav className="mt-auto flex flex-col items-center gap-4 px-2 sm:py-5">
        <Tooltip>
          <TooltipTrigger asChild={false}>
            <NavLink path={settingsNavItem.path} text={settingsNavItem.text}>
              <settingsNavItem.IconType className="h-5 w-5" />
            </NavLink>
          </TooltipTrigger>
          <TooltipContent side="right">{settingsNavItem.text}</TooltipContent>
        </Tooltip>
      </nav>
    </TooltipProvider>
  );
};

export default MainNav;
