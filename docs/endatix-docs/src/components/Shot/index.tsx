import { ThemedComponent } from "@docusaurus/theme-common";
import React from "react";

type ShotProps = Readonly<{
  alt: string;
  /** Light/dark pair, for Hub UI that has both themes. */
  sources?: { light: string; dark: string };
  /** Single image, for UI that renders one way only (public respondent pages). */
  src?: string;
  className?: string;
}>;

/**
 * A documentation screenshot.
 *
 * Captures are taken at `deviceScaleFactor: 2`, so a file's pixel width is twice the width the
 * UI really had on screen. Left alone, Infima stretches every image to the content column, and a
 * small crop ends up rendered at up to 2x life size - a 506px dialog shown 943px wide. Declaring
 * the file as a 2x source fixes that at the root: the browser lays it out at half its pixel size,
 * which is exactly life size, and `max-width: 100%` still shrinks anything wider than the column.
 * No per-image width attribute to set, and nothing to re-derive when a screenshot is recropped.
 *
 * Only for 2x captures. A legacy 1x PNG passed here would render at half size - use a plain
 * `<img>` for those until they are recaptured.
 */
export default function Shot({
  alt,
  sources,
  src,
  className,
}: ShotProps): React.ReactNode {
  if (src) {
    return <img src={src} srcSet={`${src} 2x`} alt={alt} className={className} />;
  }

  if (!sources) {
    throw new Error("<Shot> needs either `src` or `sources`.");
  }

  return (
    <ThemedComponent className={className}>
      {({ theme, className: themeClassName }) => (
        <img
          src={sources[theme]}
          srcSet={`${sources[theme]} 2x`}
          alt={alt}
          className={themeClassName}
        />
      )}
    </ThemedComponent>
  );
}
