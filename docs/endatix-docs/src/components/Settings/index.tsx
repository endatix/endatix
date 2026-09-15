import React from "react";

type SettingsProps = Readonly<{
  children: React.ReactNode;
}>;

/** Two-column config-key list; stacks below the Docusaurus 996px breakpoint. */
export default function Settings({ children }: SettingsProps): React.ReactNode {
  return <div className="edx-settings">{children}</div>;
}
