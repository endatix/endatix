import React from "react";

type SettingsProps = Readonly<{
  children: React.ReactNode;
}>;

/**
 * Reference list for configuration keys — env vars, `appsettings.json` paths, Helm values.
 *
 * A table forces every key into the same fixed columns, and the description column then
 * wraps into a wall of text at doc width. This keeps the scannable two-column alignment on
 * wide screens and stacks below Docusaurus's 996px breakpoint, where a table would overflow.
 */
export default function Settings({ children }: SettingsProps): React.ReactNode {
  return <div className="edx-settings">{children}</div>;
}
