"use client";

import { Input } from "@/components/ui/input";
import { Search } from "lucide-react";
import { comingSoonMessage } from "../teasers/coming-soon-link";
import { toast } from "sonner";

const MainSearchBar = () => {
  return (
    <div
      aria-label="main-search-bar"
      className="relative ml-auto flex-1 md:grow-0"
    >
      <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
      <ComingSoonInput />
    </div>
  );
};

const ComingSoonInput = () => {
  return (
    <Input
      type="search"
      placeholder="Search..."
      className="w-full rounded-lg bg-background pl-8 md:w-[200px] lg:w-[336px]"
      onKeyDown={() => {
        toast(comingSoonMessage);
      }}
    />
  );
};

export default MainSearchBar;
