import { INavItem } from "@/types/navigation-models";
import {
  BrainCircuit,
  Blocks,
  Home,
  LineChart,
  Settings,
  TextCursorInput,
  Users2,
} from "lucide-react";

const sitemapArray: INavItem[] = [
  {
    text: "Home",
    path: "/",
    IconType: Home,
  },
  {
    text: "Forms",
    path: "/",
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

export const comingSoonMessage : string = "We are working hard to provide great features to you. Stay tuned. Great stuff is on the way!";

export const sitemap: Sitemap = sitemapArray.reduce((acc, item) => {
  acc[item.text] = item;
  return acc;
}, {} as Sitemap);