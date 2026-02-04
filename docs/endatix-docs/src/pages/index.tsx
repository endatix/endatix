import Link from "@docusaurus/Link";
import useDocusaurusContext from "@docusaurus/useDocusaurusContext";
import HomepageFeatures from "@site/src/components/HomepageFeatures";
import Heading from "@theme/Heading";
import Layout from "@theme/Layout";
import clsx from "clsx";
import { Code2, LayoutDashboard } from "lucide-react";

import styles from "./index.module.css";

function HomepageHeader() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <header className={clsx("hero hero--primary", styles.heroBanner)}>
      <div className="container">
        <Heading as="h1" className="hero__title">
          Welcome to Endatix Documentation
        </Heading>
        <p className="hero__subtitle">{siteConfig.tagline}</p>
      </div>
    </header>
  );
}

function HeroCard({
  icon: Icon,
  title,
  description,
  primaryCTA,
  primaryLink,
  secondaryLinks,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  description: string;
  primaryCTA: string;
  primaryLink: string;
  secondaryLinks: Array<{ label: string; href: string }>;
}) {
  return (
    <div className={styles.heroCard}>
      <div className={styles.heroCardIcon}>
        <Icon size={48} />
      </div>
      <Heading as="h3" className={styles.heroCardTitle}>
        {title}
      </Heading>
      <p className={styles.heroCardDescription}>{description}</p>
      <div className={styles.heroCardActions}>
        <Link className="button button--primary button--lg" to={primaryLink}>
          {primaryCTA}
        </Link>
        <div className={styles.heroCardSecondaryLinks}>
          {secondaryLinks.map((link, idx) => (
            <Link key={idx} className={styles.secondaryLink} to={link.href}>
              {link.label}
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}

function ProductDeepDive({
  title,
  description,
  link,
}: {
  title: string;
  description: string;
  link: string;
}) {
  return (
    <div className={styles.productDeepDive}>
      <Heading as="h3" className={styles.productDeepDiveTitle}>
        {title}
      </Heading>
      <p className={styles.productDeepDiveDescription}>{description}</p>
      <Link className="button button--secondary" to={link}>
        Learn More
      </Link>
    </div>
  );
}

function HomepageContent() {
  return (
    <div className="container margin-top--xl margin-bottom--xl">
      {/* Hero Cards Section */}
      <div className={styles.heroCardsSection}>
        <div className="row">
          <div className="col col--6">
            <HeroCard
              icon={Code2}
              title="I am a Developer"
              description="Build, extend, and integrate. Dive into our .NET API and Next.js Hub to create powerful data collection solutions."
              primaryCTA="Quick Start Guide"
              primaryLink="/docs/getting-started/quick-start"
              secondaryLinks={[
                {
                  label: "API Reference",
                  href: "/docs/developers/api/api-reference",
                },
                { label: "GitHub", href: "https://github.com/endatix" },
              ]}
            />
          </div>
          <div className="col col--6">
            <HeroCard
              icon={LayoutDashboard}
              title="I am an End User"
              description="Create forms, manage responses, and analyze data. Learn how to use the Endatix Hub UI to power your business."
              primaryCTA="Hub Basics"
              primaryLink="/docs/end-users/overview"
              secondaryLinks={[]}
            />
          </div>
        </div>
      </div>

      {/* Product Deep Dives Section */}
      <div className={styles.productDeepDivesSection}>
        <Heading as="h2" className="text--center margin-bottom--lg">
          Product Deep Dives
        </Heading>
        <div className="row">
          <div className="col col--6">
            <ProductDeepDive
              title="Endatix API (.NET)"
              description="The engine. Open-source, high-performance backend for form logic and data storage."
              link="/docs/developers/api/"
            />
          </div>
          <div className="col col--6">
            <ProductDeepDive
              title="Endatix Hub (Next.js)"
              description="The interface. A sophisticated, enterprise-ready UI for managing the entire Endatix lifecycle."
              link="/docs/developers/hub/"
            />
          </div>
        </div>
      </div>

      {/* FAQs Section - Keep for now */}
      <div className="margin-top--xl">
        <Heading as="h2" className="text--center margin-bottom--lg">
          FAQs
        </Heading>
        <div className="row">
          <div className="col col--6">
            <Heading as="h3">Is it free for commercial use?</Heading>
            <p>
              Yes, Endatix API is a free and open-source library, licensed under
              the MIT License and may be used for commercial projects. Endatix
              Hub requires a commercial license for production use.
            </p>
          </div>
          <div className="col col--6">
            <Heading as="h3">Which databases are supported?</Heading>
            <p>
              Currently, Endatix supports Microsoft SQL Server and PostgreSQL.
              We plan to continue adding support for other types.
            </p>
          </div>
        </div>
        <div className="row">
          <div className="col col--6">
            <Heading as="h3">Do I need to be on the .NET stack?</Heading>
            <p>
              No. While the platform is written in C#, we provide a
              containerized version, which you can run alongside your solution
              in any Docker-compatible environment.
            </p>
          </div>
          <div className="col col--6">
            <Heading as="h3">Do I need a SurveyJS license?</Heading>
            <p>
              No, the SurveyJS Form library is also licensed under the MIT
              open-source license and is free for commercial use.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function Home(): React.ReactNode {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title={`${siteConfig.title} - Master the art of data collection`}
      description="Master the art of data collection. Whether you are building custom workflows with our API or managing forms in the Hub, we have you covered."
    >
      <HomepageHeader />
      <main>
        <HomepageContent />
        <HomepageFeatures />
      </main>
    </Layout>
  );
}
