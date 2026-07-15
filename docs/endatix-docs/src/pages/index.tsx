import Link from "@docusaurus/Link";
import useDocusaurusContext from "@docusaurus/useDocusaurusContext";
import HomepageFeatures from "@site/src/components/HomepageFeatures";
import Heading from "@theme/Heading";
import Layout from "@theme/Layout";
import { Code2, LayoutDashboard, type LucideIcon } from "lucide-react";

import styles from "./index.module.css";

function HomepageHeader() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <header className={styles.heroSection}>
      <div className={styles.heroGrid} aria-hidden="true" />
      <div className="container">
        <div className={styles.heroContent}>
          <Heading as="h1" className={styles.heroTitle}>
            Endatix Documentation Portal
          </Heading>
          <p className={styles.heroSubtitle}>{siteConfig.tagline}</p>
        </div>
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
  icon: LucideIcon;
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
      <Link className="button button--primary button--lg" to={link}>
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
              primaryCTA="Endatix Hub Docs"
              primaryLink="/docs/end-users/forms"
              secondaryLinks={[{
                  label: "Form Builder",
                  href: "/docs/end-users/forms/form-builder",
                },
              {
                  label: "Logic Expressions",
                  href: "/docs/end-users/forms/form-builder/logic-expressions",
                }]}
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
    </div>
  );
}

export default function Home(): React.ReactNode {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title="Endatix Documentation Portal"
      description="Endatix makes it easy to launch your own form management platform, packed with features that rival popular SaaS tools such as Qualtrics, Typeform, Formstack, and others."
    >
      <HomepageHeader />
      <main>
        <HomepageContent />
      </main>
    </Layout>
  );
}
