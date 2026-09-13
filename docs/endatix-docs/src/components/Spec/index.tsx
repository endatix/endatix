import React from "react";

type SpecProps = Readonly<{
  label: string;
  children: React.ReactNode;
}>;

export default function Spec({ label, children }: SpecProps): React.ReactNode {
  return (
    <>
      <dt>{label}</dt>
      <dd>{children}</dd>
    </>
  );
}
