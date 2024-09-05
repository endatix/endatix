"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { INavItem, ISitemapItem } from "@/types/navigation-models";
import React from "react";

type BreadcrumbNavProps = {
  sitemap: ISitemapItem[];
  homeText?: string;
  listClasses?: string;
  activeClasses?: string;
  capitalizeLinks?: boolean;
};

const BreadcrumbNav = ({
  homeText,
  sitemap,
  listClasses,
  activeClasses,
  capitalizeLinks = true,
}: BreadcrumbNavProps) => {
  const currentPath = usePathname();
  const pathNames = currentPath.split("/").filter((path) => path);

  return (
    <Breadcrumb className="hidden md:flex">
      <BreadcrumbList>
        {homeText?.length && (
          <>
            <BreadcrumbItem>
              <BreadcrumbLink asChild>
                <Link href="/">{homeText}</Link>
              </BreadcrumbLink>
            </BreadcrumbItem>
            {pathNames?.length > 0 && <BreadcrumbSeparator />}
          </>
        )}
        {pathNames.map((link, index) => {
          const href = `/${pathNames.slice(0, index + 1).join("/")}`;
          const itemClasses =
            currentPath === href
              ? `${listClasses} ${activeClasses}`
              : listClasses;
          const parsedLink = link.replace("-", " ");
          const itemLink = capitalizeLinks
            ? parsedLink[0].toUpperCase() + parsedLink.slice(1, parsedLink.length)
            : parsedLink;
          return (
            <React.Fragment key={index}>
              <BreadcrumbItem>
                <BreadcrumbLink className={itemClasses} asChild>
                  <Link href={href}>{itemLink}</Link>
                </BreadcrumbLink>
              </BreadcrumbItem>
              {pathNames.length !== index + 1 && <BreadcrumbSeparator />}
            </React.Fragment>
          );
        })}
      </BreadcrumbList>
    </Breadcrumb>
  );
};

export default BreadcrumbNav;
