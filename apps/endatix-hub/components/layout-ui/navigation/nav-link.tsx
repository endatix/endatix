"use client";

import Link from "next/link";
import { ReactNode, useMemo } from "react";
import { usePathname } from "next/navigation";
import { showComingSoonMessage } from "../teasers/coming-soon-link";

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

  const ALLOWED_PAGES = ["/", "/forms", "/settings/security"];

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
      onClick={() => {
        if (!ALLOWED_PAGES.some((allowedPath) => allowedPath === path)) {
          showComingSoonMessage();
        }
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
