"use client";

import Image from "next/image";
import { Button } from "@/components/ui/button";
import { showComingSoonMessage } from "../teasers/coming-soon-link";

const AvatarIcon = () => {
  return (
    <>
      <Button
        variant="outline"
        aria-label="my-account-menu"
        size="icon"
        className="overflow-hidden rounded-full"
        onClick={(e) => {
          e.preventDefault();
          showComingSoonMessage(e);
        }}
      >
        <Image
          src="/placeholder-user.jpg"
          width={36}
          height={36}
          alt="Avatar"
          className="overflow-hidden rounded-full"
        />
      </Button>
    </>
  );
};

export default AvatarIcon;
