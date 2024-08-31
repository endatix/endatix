import { INavItem } from "@/types/navigation-models";
import {
  BrainCircuit,
  FileText,
  Home,
  LineChart,
  Settings,
  TextCursorInput,
  Users2,
} from "lucide-react";

const sitemapArray: INavItem[] = [
  {
    text: "Dashboard",
    path: "/",
    IconType: Home,
  },
  {
    text: "Forms",
    path: "/dashboard",
    IconType: TextCursorInput,
  },
  {
    text: "Submissions",
    path: "/submissions",
    IconType: FileText,
  },
  {
    text: "Customers",
    path: "/customers",
    IconType: Users2,
  },
  {
    text: "Analytics",
    path: "/analytics",
    IconType: LineChart,
  },
  {
    text: "Settings",
    path: "/settings",
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