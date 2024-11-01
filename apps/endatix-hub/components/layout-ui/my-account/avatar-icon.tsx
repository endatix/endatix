"use client";

import { Button } from "@/components/ui/button";
import { showComingSoonMessage } from "../teasers/coming-soon-link";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";

const AvatarIcon = () => {
  return (
      <Button
        variant="outline"
        aria-label="my-account-menu"
        size="icon"
        className="overflow-hidden rounded-full w-9 h-9"
        onClick={(e) => {
          e.preventDefault();
          showComingSoonMessage(e);
        }}
      >
        <Avatar>
          <AvatarImage src="https://github.com/shadcn.png" alt="@endatix" />
          <AvatarFallback>EDX</AvatarFallback>
        </Avatar>
      </Button>
  );
};

export default AvatarIcon;
