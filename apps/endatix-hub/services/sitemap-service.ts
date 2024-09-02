import { sitemap } from "@/lib/constants";
import { INavItem, ISitemapItem } from "@/types/navigation-models";
import { BrainCircuit } from "lucide-react";

export class SitemapService {
  public static getSitemap(): ISitemapItem[] {
    const sitemapArray : ISitemapItem[] =  Object.entries(sitemap).map(([key, value]) => {
      const sitemapItem : ISitemapItem = {
        text : value.text,
        path : value.path
      };
      return sitemapItem
    });
    return sitemapArray;
  }

  public static getTopLevelSitemap(excludeSettings: boolean = false) :  INavItem[] {
    const sitemapList: INavItem[] = [
      sitemap.Home,
      sitemap.Forms,
      sitemap.Customers,
      sitemap.Analytics,
      sitemap.Integrations,
    ];

    if (!excludeSettings){
      sitemapList.push(sitemap.Settings);
    }

    return sitemapList;
  }

  public static getLogo(): INavItem {
    const logoNavItem: INavItem = {
      path: "https://endatix.com",
      text: "Endatix",
      IconType: BrainCircuit,
    };

    return logoNavItem;
  }
}
