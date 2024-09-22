"use client";

import { Input } from "@/components/ui/input";
import { Search } from "lucide-react";
import { showComingSoonMessage } from "../utils/coming-soon-message";

const MainSearchBar = () => {
  return (
    <>
      <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
      <Input
        type="search"
        placeholder="Search..."
        className="w-full rounded-lg bg-background pl-8 md:w-[200px] lg:w-[336px]"
        onKeyDown={ (e) => {
            e.preventDefault();
            showComingSoonMessage(e);
        }}
      />
    </>
  );
};

export default MainSearchBar;