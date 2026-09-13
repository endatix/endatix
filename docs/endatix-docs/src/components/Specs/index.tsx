import React from "react";

type SpecsProps = Readonly<{
  children: React.ReactNode;
}>;

export default function Specs({ children }: SpecsProps): React.ReactNode {
  return <dl className="edx-specs">{children}</dl>;
}
