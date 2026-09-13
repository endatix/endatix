import {
  Boxes,
  Database,
  LayoutDashboard,
  Package,
  Rocket,
  Server,
  type LucideIcon,
} from "lucide-react";
import React from "react";

/**
 * Icon set for doc cards (`edx-card`, `edx-linkcard`).
 *
 * Extend it with a named import plus a registry entry — a wildcard import or a
 * dynamic `lucide-react` lookup would pull the entire Lucide set into the bundle.
 */
const ICONS = {
  boxes: Boxes,
  database: Database,
  dashboard: LayoutDashboard,
  package: Package,
  rocket: Rocket,
  server: Server,
} satisfies Record<string, LucideIcon>;

export type IconName = keyof typeof ICONS;
export type IconAccent = "brand" | "violet" | "green" | "amber";

type IconProps = {
  name: IconName;
  /** Tile tint. Defaults to the brand blue — vary it only to separate peers. */
  accent?: IconAccent;
};

/**
 * Renders the tinted tile that sits in a card header, not a bare glyph.
 * Decorative by design: the card title carries the meaning.
 */
export default function Icon({
  name,
  accent = "brand",
}: IconProps): React.ReactNode {
  const Glyph = ICONS[name];

  if (!Glyph) {
    throw new Error(
      `Unknown icon "${name}". Add it to src/components/Icon/index.tsx.`,
    );
  }

  return (
    <span className="edx-card__icon" data-accent={accent}>
      <Glyph size={20} strokeWidth={2} aria-hidden="true" />
    </span>
  );
}
