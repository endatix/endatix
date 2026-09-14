import React from "react";
import clsx from "clsx";
import type { Props } from "@theme/NotFound/Content";
import Heading from "@theme/Heading";
import CardGrid from "@site/src/components/CardGrid";
import LinkCard from "@site/src/components/LinkCard";
import Sheep from "@site/src/components/Sheep";

/** Swizzled classic NotFound body. Layout / SEO stay in the default wrapper. */
export default function NotFoundContent({ className }: Props): React.ReactNode {
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
