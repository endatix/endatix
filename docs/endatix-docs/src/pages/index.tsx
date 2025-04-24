import Link from "@docusaurus/Link";
import useDocusaurusContext from "@docusaurus/useDocusaurusContext";
import HomepageFeatures from "@site/src/components/HomepageFeatures";
import Heading from "@theme/Heading";
import Layout from "@theme/Layout";
import clsx from "clsx";

import styles from "./index.module.css";

function HomepageHeader() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <header className={clsx("hero hero--primary", styles.heroBanner)}>
      <div className="container">
        <Heading as="h1" className="hero__title">
          {siteConfig.title}
        </Heading>
        <p className="hero__subtitle">{siteConfig.tagline}</p>
        <div className={styles.buttons}>
          <Link
            className="button button--secondary button--lg"
            to="/docs/getting-started/what-is-endatix"
          >
            What is Endatix?
          </Link>
        </div>
      </div>
    </header>
  );
}

function HomepageContent() {
  return (
    <div className="container margin-top--xl margin-bottom--xl">
      <div className="row">
        <div className="col col--10 col--offset-1">
          <div className="margin-bottom--lg">
            <Heading as="h2">Open-source backend for SurveyJS</Heading>
            <p>
              Endatix is a free and open-source backend for SurveyJS projects
              that can be integrated into any .NET Core project or set up in a
              container as a standalone application. Its database persistence
              provider makes it easy to store JSON form schema and form
              submissions. It also features a REST API that enables any frontend
              to be paired with it, regardless of whether it uses React, Vue,
              Angular, or JQuery.
            </p>
          </div>

          <div className="margin-bottom--lg">
            <Heading as="h2">Features that Save Time</Heading>
            <div className="row">
              <div className="col col--6">
                <Heading as="h3">Fluent API</Heading>
                <p>
                  Chained configuration methods for a cleaner and more readable
                  initialization.
                </p>
              </div>
              <div className="col col--6">
                <Heading as="h3">Persistence of Forms and Submissions</Heading>
                <p>
                  Persist SurveyJS form schema and submissions to a database
                  with ease.
                </p>
              </div>
            </div>
            <div className="row">
              <div className="col col--6">
                <Heading as="h3">REST API Endpoints</Heading>
                <p>
                  The API layer provides endpoints for SurveyJS CRUD operations.
                </p>
              </div>
              <div className="col col--6">
                <Heading as="h3">Event Handling</Heading>
                <p>
                  Subscribe to form submission events and trigger workflows.
                </p>
              </div>
            </div>
          </div>

          <div className="margin-bottom--lg">
            <Heading as="h2">Get Started in Minutes</Heading>
            <p>
              Integrate Endatix into your .NET Core project with your favorite
              package manager or download it directly from NuGet.
            </p>
            <div className={styles.buttons}>
              <Link
                className="button button--primary button--lg margin-right--md"
                to="/docs/getting-started/installation"
              >
                Installation Guide
              </Link>
            </div>
          </div>

          <div>
            <Heading as="h2">FAQs</Heading>
            <div className="row">
              <div className="col col--6">
                <Heading as="h3">Is it free for commercial use?</Heading>
                <p>
                  Yes, Endatix is a free and open-source library, licensed under
                  the MIT License and may be used for commercial projects.
                </p>
              </div>
              <div className="col col--6">
                <Heading as="h3">Which databases are supported?</Heading>
                <p>
                  Currently, Endatix supports Microsoft SQL Server and
                  PostgreSQL. We plan to continue adding support for other
                  types.
                </p>
              </div>
            </div>
            <div className="row">
              <div className="col col--6">
                <Heading as="h3">Do I need to be on the .NET stack?</Heading>
                <p>
                  No. While the platform is written in C#, we provide a
                  containerized version, which you can run alongside your
                  solution in any Docker-compatible environment.
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
      </div>
    </div>
  );
}

export default function Home(): React.ReactNode {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title={`${siteConfig.title} - Open-source backend for SurveyJS`}
      description="Endatix is a free and open-source backend for SurveyJS projects that can be integrated into any .NET Core project or set up in a container as a standalone application."
    >
      <HomepageHeader />
      <main>
        <HomepageContent />
        <HomepageFeatures />
      </main>
    </Layout>
  );
}
