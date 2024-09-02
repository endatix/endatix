"use client";
import Link from "next/link";
import { ReactNode, useMemo } from "react";
import { usePathname } from "next/navigation";
import { toast } from "sonner";
import { comingSoonMessage } from "@/lib/constants";

type NavLinkProps = {
  path: string;
  text: string;
  setIsActive?: boolean;
  className?: string;
  activeClassName?: string;
  children: ReactNode;
};

const NavLink = ({
  text,
  path,
  setIsActive = true,
  children,
  className = "flex h-9 w-9 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:text-foreground md:h-8 md:w-8",
  activeClassName = "bg-accent text-accent-foreground",
}: NavLinkProps) => {
  const currentPath = usePathname();

  const isActive = useMemo(() => {
    if (!setIsActive || !path.startsWith("/")) {
      return false;
    }
    return currentPath === path;
  }, [setIsActive, path, currentPath]);

  const _className = useMemo(() => {
    return isActive ? `${className} ${activeClassName}` : `${className}`;
  }, [activeClassName, className, isActive]);

  return (
    <Link
      onClick={(e) => {
        toast(comingSoonMessage);
      }}
      href={path}
      className={_className}
    >
      {children}
      <span className="sr-only">{text}</span>
    </Link>
  );
};

export default NavLink;
