import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  imgUrl: string;
  description: JSX.Element;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'Forms Management',
    imgUrl: require('@site/static/img/forms-design.png').default,
    description: (
      <>
        Effortlessly define and manage complex SurveyJS forms with advanced capabilities.
      </>
    ),
  },
  {
    title: 'Top Dev Experience',
    imgUrl: require('@site/static/img/forms-integrate.png').default,
    description: (
      <>
        Seamlessly integrate into your software products for a smooth and efficient developer experience.
      </>
    ),
  },
  {
    title: 'End-to-end Data',
    imgUrl: require('@site/static/img/forms-data.png').default,
    description: (
      <>
        Collect, store, and process data securely and efficiently with endless customization possibilities.
      </>
    ),
  },
];

function Feature({title, imgUrl, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center">
        <img className={styles.featureSvg} src={imgUrl} />
      </div>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): JSX.Element {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
