import clsx from "clsx";
import React from "react";

type PillProps = Readonly<{
  required?: boolean;
  children: React.ReactNode;
}>;

export default function Pill({
  required = false,
  children,
}: PillProps): React.ReactNode {
  return (
    <span className={clsx("edx-pill", required && "edx-pill--required")}>
      {children}
    </span>
  );
}
