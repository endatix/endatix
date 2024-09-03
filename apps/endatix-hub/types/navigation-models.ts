import { LucideIcon } from "lucide-react";

interface ISitemapItem {
  text: string;
  path: string;
}
interface INavItem extends ISitemapItem {
  IconType: LucideIcon;
  children?: INavItem[];
}

export type { ISitemapItem, INavItem };
