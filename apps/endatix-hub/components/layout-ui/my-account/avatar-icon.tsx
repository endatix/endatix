"use client";

import Image from "next/image";

const AvatarIcon = () => {
  return (
    <Image
      src="/placeholder-user.jpg"
      width={36}
      height={36}
      alt="Avatar"
      className="overflow-hidden rounded-full"
    />
  );
};

export default AvatarIcon;
