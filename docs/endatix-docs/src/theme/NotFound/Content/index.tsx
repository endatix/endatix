import Heading from "@theme/Heading";
import type { Props } from "@theme/NotFound/Content";
import clsx from "clsx";
import React, { type ReactNode } from "react";
import CardGrid from "@site/src/components/CardGrid";
import LinkCard from "@site/src/components/LinkCard";
import Sheep from "@site/src/components/Sheep";

/**
 * A 404 that should never need scrolling: the sheep sits beside the copy rather
 * than above it, and the only way out is the two doc roots the navbar offers.
 * Below 720px the row stacks and re-centres.
 *
 * The numeral is decoration - the headline is the h1.
 */
export default function NotFoundContent({ className }: Props): ReactNode {
  return (
    <main className={clsx("container margin-vert--lg edx-notfound", className)}>
      <div className="edx-notfound__hero">
        <Sheep />

        <div className="edx-notfound__copy">
          <p className="edx-notfound__code" aria-hidden="true">
            404
          </p>

          <Heading as="h1" className="edx-notfound__title">
            Looks like you&apos;ve drifted off course
          </Heading>

          <p className="edx-notfound__lede">
            This page has been moved, deleted, or never existed.
          </p>
        </div>
      </div>

      <CardGrid compact>
        <LinkCard
          to="/docs/getting-started"
          icon="rocket"
          title="Developer docs"
          description="Install, configure, and build on Endatix."
        />
        <LinkCard
          to="/docs/end-users/forms"
          icon="dashboard"
          title="End-user guides"
          description="Build and manage forms in the Endatix Hub."
        />
      </CardGrid>
    </main>
  );
}
