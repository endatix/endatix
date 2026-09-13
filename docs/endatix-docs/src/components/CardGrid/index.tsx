import clsx from "clsx";
import React from "react";

type CardGridProps = Readonly<{
  compact?: boolean;
  children: React.ReactNode;
}>;

export default function CardGrid({
  compact = false,
  children,
}: CardGridProps): React.ReactNode {
  return (
    <div className={clsx("edx-grid", compact && "edx-grid--compact")}>
      {children}
    </div>
  );
}
