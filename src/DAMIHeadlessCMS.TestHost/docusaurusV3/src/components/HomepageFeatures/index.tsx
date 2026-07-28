import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';
import config from '@generated/docusaurus.config';

type FeatureItem = {
  title: string;
  Svg: React.ComponentType<React.ComponentProps<'svg'>>;
  description: ReactNode;
  btnHref: string;
  btnDesc: string;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'Stipendi',
    Svg: require('@site/static/img/money.svg').default,
    description: (
      <>
        Nuovo regolamento sugli stipendi/ingaggi dei calciatori 
      </>
    ),
    btnHref: config.baseUrl + 'docs/Le%20finanze/5.2.%20Stipendi',
    btnDesc: 'Vai alla sezione',
  },
  {
    title: 'Mercato',
    Svg: require('@site/static/img/market.svg').default,
    description: (
      <>
        Sessioni, regole generali, penalizzazioni, gruppo whatsapp
      </>
    ),
    btnHref: config.baseUrl + 'docs/Mercato/2.1.%20Aste',
    btnDesc: 'Vai alla sezione',
  },
  {
    title: 'Contratti',
    Svg: require('@site/static/img/contract.svg').default,
    description: (
      <>
        Regolamento su <br/>contratto giocatori
      </>
    ),
    btnHref: config.baseUrl + 'docs/Contratti%20dei%20calciatori/3.1.%20Anni%20di%20contratto%20e%20rinnovo',
    btnDesc: 'Vai alla sezione',
  },
  {
    title: 'Morale',
    Svg: require('@site/static/img/morale.svg').default,
    description: (
      <>
        Morale dei calciatori <br/>e ranking
      </>
    ),
    btnHref: config.baseUrl + 'docs/Morale%20dei%20calciatori/4.1.%20Morale',
    btnDesc: 'Vai alla sezione',
  },
];

function Feature({title, Svg, description, btnHref, btnDesc}: FeatureItem) {
  return (
    <div className={clsx('col col--3')}>
      <div className="text--center">
        <Svg className={styles.featureSvg} role="img" />
      </div>
      <div className="text--center padding-horiz--md">
        <h3>{title}</h3>
        <p>{description}</p>
        <a className="button button--secondary button--lg" href={btnHref}>{btnDesc}</a>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
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
