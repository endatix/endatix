"use client";

import Image from "next/image";
import { Button } from "@/components/ui/button";
import { comingSoonMessage } from "@/lib/constants";
import { toast } from "sonner";

const AvatarIcon = () => {
  return (
    <>
      <Button
        variant="outline"
        size="icon"
        className="overflow-hidden rounded-full"
        onClick={(e) => {
          e.preventDefault();
          toast(comingSoonMessage);
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
