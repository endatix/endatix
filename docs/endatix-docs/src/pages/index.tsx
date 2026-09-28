import Link from "@docusaurus/Link";
import useDocusaurusContext from "@docusaurus/useDocusaurusContext";
import HomepageFeatures from "@site/src/components/HomepageFeatures";
import Heading from "@theme/Heading";
import Layout from "@theme/Layout";
import {
  Code2,
  CodeXml,
  Download,
  GitBranch,
  LayoutDashboard,
  Server,
  type LucideIcon,
} from "lucide-react";

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

function TaskCard({
  icon: Icon,
  title,
  description,
  link,
}: {
  icon: LucideIcon;
  title: string;
  description: string;
  link: string;
}) {
  return (
    <Link className={styles.taskCard} to={link}>
      <div className={styles.taskCardIcon}>
        <Icon size={22} aria-hidden="true" />
      </div>
      <Heading as="h3" className={styles.taskCardTitle}>
        {title}
      </Heading>
      <div className={styles.taskCardDescription}>{description}</div>
    </Link>
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
                { label: "Configuration", href: "/docs/configuration" },
                { label: "Webhooks", href: "/docs/guides/webhooks" },
                { label: "GitHub", href: "https://github.com/endatix" },
              ]}
            />
          </div>
          <div className="col col--6">
            <HeroCard
              icon={LayoutDashboard}
              title="I am an End User"
              description="Create forms, manage responses, and analyze data. Learn how to use the Endatix Hub UI to power your business."
              primaryCTA="Platform Overview"
              primaryLink="/docs/end-users"
              secondaryLinks={[
                { label: "Forms", href: "/docs/end-users/forms" },
                {
                  label: "Form Builder",
                  href: "/docs/end-users/forms/form-builder",
                },
                {
                  label: "Question Types",
                  href: "/docs/end-users/forms/form-builder/question-types",
                },
                {
                  label: "Logic Expressions",
                  href: "/docs/end-users/forms/form-builder/logic-expressions",
                },
              ]}
            />
          </div>
        </div>
      </div>

      {/* Common Tasks Section */}
      <div className={styles.taskCardsSection}>
        <Heading as="h2" className="text--center margin-bottom--lg">
          Common tasks
        </Heading>
        <div className="row">
          <div className="col col--3">
            <TaskCard
              icon={CodeXml}
              title="Embed a form on your site"
              description="Drop a form into a page with an iframe, size it, and pass data in."
              link="/docs/guides/embed-form-via-iframe"
            />
          </div>
          <div className="col col--3">
            <TaskCard
              icon={GitBranch}
              title="Show questions conditionally"
              description="Reveal, hide, or require questions based on earlier answers."
              link="/docs/end-users/forms/form-builder/conditional-logic"
            />
          </div>
          <div className="col col--3">
            <TaskCard
              icon={Download}
              title="Export your responses"
              description="Download submissions as CSV, Excel, or JSON, or generate a codebook."
              link="/docs/end-users/submissions/exporting-submissions"
            />
          </div>
          <div className="col col--3">
            <TaskCard
              icon={Server}
              title="Self-host Endatix"
              description="Run the API and Hub on your own infrastructure with Docker."
              link="/docs/building-your-solution/deployment/self-hosting"
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
      description="Technical and end-user guides for the Endatix API and Hub form management applications. Find tutorials, API references, and integration steps."
    >
      <HomepageHeader />
      <main>
        <HomepageContent />
      </main>
    </Layout>
  );
}
