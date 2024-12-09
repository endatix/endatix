"use client";

import { Avatar, AvatarFallback, AvatarImage } from "../ui/avatar"

const isEndatixUser = (username?: string) => username?.includes("endatix") ?? false;

interface UserAvatarProps {
  isLoggedIn?: boolean;
  userName?: string;
  className?: string;
  onClick?: () => void;
}

const UserAvatar: React.FC<UserAvatarProps> = ({
  userName,
  isLoggedIn = false,
  className,
  onClick }) => {
  const determineImageUrl = () => {
    if (!isLoggedIn) {
      return "/assets/images/avatars/placeholder-user.jpg";
    }

    if (isEndatixUser(userName)) {
      return "/assets/images/avatars/oggy_avatar.jpg";
    }

    return "/assets/images/avatars/placeholder-user.jpg";
  };

  const imageUrl = determineImageUrl();

  return (
    <Avatar className={className} onClick={onClick}>
      <AvatarImage src={imageUrl} alt="{username}" />
      <AvatarFallback>EDX</AvatarFallback>
    </Avatar>
  );
};

export default UserAvatar;
