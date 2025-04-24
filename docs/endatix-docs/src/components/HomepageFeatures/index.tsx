import clsx from "clsx";
import React from "react";
import styles from "./styles.module.css";

type FeatureItem = {
  title: string;
  imgUrl: string;
  description: React.ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: "Forms Management",
    imgUrl: require("@site/static/img/forms-design.png").default,
    description: (
      <>
        Effortlessly define and manage complex SurveyJS forms with advanced
        capabilities.
      </>
    ),
  },
  {
    title: "Top Dev Experience",
    imgUrl: require("@site/static/img/forms-integrate.png").default,
    description: (
      <>
        Seamlessly integrate into your software products for a smooth and
        efficient developer experience.
      </>
    ),
  },
  {
    title: "End-to-end Data",
    imgUrl: require("@site/static/img/forms-data.png").default,
    description: (
      <>
        Collect, store, and process data securely and efficiently with endless
        customization possibilities.
      </>
    ),
  },
];

export default function HomepageFeatures(): React.ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((feature, idx) => (
            <div key={idx} className={clsx("col col--4")}>
              <div className={styles.featureCard}>
                <div className="text--center">
                  <img
                    className={styles.featureSvg}
                    src={feature.imgUrl}
                    alt={feature.title}
                  />
                </div>
                <div className="text--center padding-horiz--md">
                  <h3 className={styles.featureTitle}>{feature.title}</h3>
                  <p className={styles.featureDescription}>
                    {feature.description}
                  </p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
