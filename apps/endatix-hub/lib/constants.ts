import { INavItem } from "@/types/navigation-models";
import {
  Blocks,
  GitCompareArrows,
  Home,
  LineChart,
  Settings,
  TextCursorInput,
  Users2,
} from "lucide-react";

export const HOME_ROUTE_PATH = "/";

const sitemapArray: INavItem[] = [
  {
    text: "Home",
    path: "/",
    IconType: Home,
  },
  {
    text: "Forms",
    path: "/forms",
    IconType: TextCursorInput,
  },
  {
    text: "Customers",
    path: "/",
    IconType: Users2,
  },
  {
    text: "Analytics",
    path: "/",
    IconType: LineChart,
  },
  {
    text: "Workflows",
    path: "/",
    IconType: GitCompareArrows,
  },
  {
    text: "Integrations",
    path: "/",
    IconType: Blocks,
  },
  {
    text: "Settings",
    path: "/",
    IconType: Settings,
  },
];

type Sitemap = {
  [key: string]: INavItem;
};

export const sitemap: Sitemap = sitemapArray.reduce((acc, item) => {
  acc[item.text] = item;
  return acc;
}, {} as Sitemap);